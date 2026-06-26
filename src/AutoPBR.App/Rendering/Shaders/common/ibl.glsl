// Genesis preview shader - generated world-space environment probe.
// Diffuse: six-direction irradiance probe so side faces are not tied to screen-space sun placement.
// Specular: generated sky/ground/sun "virtual cubemap" prefiltered by roughness.

#ifndef GENESIS_IBL_GLSL
#define GENESIS_IBL_GLSL

//!include "common.glsl"
//!include "brdf.glsl"

vec3 fakeIblSky(vec3 dir, vec3 skyTint, vec3 groundTint)
{
    float t = clamp(dir.y * 0.5 + 0.5, 0.0, 1.0);
    return mix(groundTint, skyTint, smoothstep(0.0, 1.0, t));
}

// Inverse of skyViewLutUv in sky_dome.glsl (keep in sync).
vec2 atmoSkyViewUvFromWorldDir(vec3 dirRaw)
{
    vec3 d = normalize(dirRaw);
    float viewZenith = acos(clamp(d.y, -1.0, 1.0)) / GEN_PI;
    float u = atan(d.x, d.z) / (2.0 * GEN_PI) + 0.5;
    return vec2(u, viewZenith);
}

vec3 sampleAtmoSkyViewRadianceLinear(vec3 dirWorld, sampler2D atmoSkyViewLut)
{
    vec2 uv = atmoSkyViewUvFromWorldDir(dirWorld);
    float u = uv.x;
    float v = clamp(uv.y, 0.0, 1.0);
    const float lutWidth = 192.0;
    float texelU = 1.0 / lutWidth;
    vec3 c0 = texture(atmoSkyViewLut, vec2(u, v)).rgb;
    if (u < texelU)
    {
        vec3 c1 = texture(atmoSkyViewLut, vec2(u + 1.0, v)).rgb;
        c0 = mix(c1, c0, u / texelU);
    }
    else if (u > 1.0 - texelU)
    {
        vec3 c1 = texture(atmoSkyViewLut, vec2(u - 1.0, v)).rgb;
        c0 = mix(c0, c1, (u - (1.0 - texelU)) / texelU);
    }

    return srgbToLinear(c0);
}

vec3 previewEnvTowardSun(vec3 lightPropagationDir)
{
    float l2 = dot(lightPropagationDir, lightPropagationDir);
    if (l2 <= GEN_EPS)
    {
        return normalize(vec3(0.35, 0.85, 0.4));
    }

    return -lightPropagationDir * inversesqrt(l2);
}

float previewEnvDayFactor(vec3 lightPropagationDir, float sunIntensity)
{
    vec3 towardSun = previewEnvTowardSun(lightPropagationDir);
    float dayFromSun = smoothstep(-0.04, 0.22, towardSun.y);
    float dayFromIntensity = smoothstep(0.08, 2.0, sunIntensity);
    return saturate1(dayFromSun * dayFromIntensity);
}

vec3 previewEnvSunWarmColor(vec3 lightPropagationDir, vec3 lightColor, float sunIntensity)
{
    vec3 towardSun = previewEnvTowardSun(lightPropagationDir);
    vec3 horizonWarm = vec3(1.0, 0.62, 0.28);
    vec3 zenithWarm = vec3(1.0, 0.96, 0.86);
    float boundedIntensity = 0.65 + 0.35 * smoothstep(1.0, 12.0, sunIntensity);
    return lightColor * mix(horizonWarm, zenithWarm, smoothstep(0.0, 0.42, max(towardSun.y, 0.0))) * boundedIntensity;
}

vec3 previewEnvSkyGroundRadiance(vec3 dir, vec3 skyTintLin, vec3 groundTintLin, int enableAtmoSky,
    sampler2D atmoSkyViewLut)
{
    vec3 d = normalize(dir);
    vec3 sky = enableAtmoSky > 0
        ? sampleAtmoSkyViewRadianceLinear(d, atmoSkyViewLut)
        : fakeIblSky(d, skyTintLin, groundTintLin);

    float below = smoothstep(0.08, -0.24, d.y);
    vec3 groundReflection = groundTintLin * 0.28;
    return mix(sky, groundReflection, below);
}

vec3 previewEnvSunRadiance(vec3 dir, vec3 lightPropagationDir, vec3 lightColor, float sunIntensity, float roughness)
{
    vec3 towardSun = previewEnvTowardSun(lightPropagationDir);
    float dayAmt = previewEnvDayFactor(lightPropagationDir, sunIntensity);
    float cosSun = max(dot(normalize(dir), towardSun), 0.0);
    float r = saturate1(roughness);

    // Generated cubemap sun: narrow for smooth metals, wide and softer for rough metals.
    float tightExp = mix(1600.0, 18.0, r);
    float broadExp = mix(96.0, 4.0, r);
    float tight = pow(cosSun, tightExp) * mix(7.5, 1.1, r);
    float broad = pow(cosSun, broadExp) * mix(0.25, 0.85, r);
    float sunScale = dayAmt * mix(0.35, 1.4, smoothstep(0.4, 10.0, sunIntensity));
    return previewEnvSunWarmColor(lightPropagationDir, lightColor, sunIntensity) * (tight + broad) * sunScale;
}

vec3 previewEnvCubemapRadiance(vec3 dir, vec3 skyTintLin, vec3 groundTintLin, vec3 lightPropagationDir,
    vec3 lightColor, float sunIntensity, float roughness, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 d = normalize(dir);
    vec3 skyGround = previewEnvSkyGroundRadiance(d, skyTintLin, groundTintLin, enableAtmoSky, atmoSkyViewLut);
    vec3 sun = previewEnvSunRadiance(d, lightPropagationDir, lightColor, sunIntensity, roughness);
    return max(skyGround + sun, vec3(0.0));
}

// Six-direction generated diffuse probe. It approximates a tiny cubemap convolution
// and keeps vertical/side faces lit from stable world-space directions.
vec3 previewAmbientProbeIrradiance(vec3 N, vec3 skyTintLin, vec3 groundTintLin, vec3 lightPropagationDir,
    vec3 lightColor, float sunIntensity, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 n = normalize(N);
    vec3 px = vec3(1.0, 0.0, 0.0);
    vec3 nx = vec3(-1.0, 0.0, 0.0);
    vec3 py = vec3(0.0, 1.0, 0.0);
    vec3 ny = vec3(0.0, -1.0, 0.0);
    vec3 pz = vec3(0.0, 0.0, 1.0);
    vec3 nz = vec3(0.0, 0.0, -1.0);

    float roughDiffuse = 1.0;
    vec3 radPx = previewEnvCubemapRadiance(px, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);
    vec3 radNx = previewEnvCubemapRadiance(nx, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);
    vec3 radPy = previewEnvCubemapRadiance(py, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);
    vec3 radNy = previewEnvCubemapRadiance(ny, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);
    vec3 radPz = previewEnvCubemapRadiance(pz, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);
    vec3 radNz = previewEnvCubemapRadiance(nz, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughDiffuse, enableAtmoSky, atmoSkyViewLut);

    float wPx = max(dot(n, px), 0.0);
    float wNx = max(dot(n, nx), 0.0);
    float wPy = max(dot(n, py), 0.0);
    float wNy = max(dot(n, ny), 0.0);
    float wPz = max(dot(n, pz), 0.0);
    float wNz = max(dot(n, nz), 0.0);

    vec3 weighted = radPx * wPx + radNx * wNx + radPy * wPy + radNy * wNy + radPz * wPz + radNz * wNz;
    float wSum = max(wPx + wNx + wPy + wNy + wPz + wNz, GEN_EPS);
    vec3 probe = weighted / wSum;

    vec3 sideAverage = (radPx + radNx + radPz + radNz) * 0.25;
    vec3 floorFill = radPy * 0.10 + sideAverage * 0.18 + radNy * 0.08;
    probe += floorFill;

    // Probe tempering: keep the brightness, retain only ~30 percent of the color cast.
    float lum = dot(probe, vec3(0.2126, 0.7152, 0.0722));
    return mix(vec3(lum), probe, 0.35);
}

vec3 fakeIblAmbientDiffuse(vec3 N, vec3 skyTint, vec3 groundTint, vec3 lightPropagationDir, vec3 lightColor,
    float sunIntensity, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    return previewAmbientProbeIrradiance(N, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        enableAtmoSky, atmoSkyViewLut);
}

// Roughness-dependent generated environment radiance (stand-in for prefiltered cubemap mips).
vec3 iblPrefilteredSkyRadiance(vec3 N, vec3 R, float roughness, vec3 skyTintLin, vec3 groundTintLin,
    vec3 lightPropagationDir, vec3 lightColor, float sunIntensity, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    float alpha = brdfGgxAlpha(roughness);

    vec3 up = abs(R.y) < 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 t = normalize(cross(up, R));
    vec3 b = cross(R, t);
    float spread = 0.035 + alpha * 1.65;
    vec3 center = previewEnvCubemapRadiance(R, skyTintLin, groundTintLin, lightPropagationDir, lightColor, sunIntensity,
        roughness, enableAtmoSky, atmoSkyViewLut);
    vec3 tap0 = previewEnvCubemapRadiance(normalize(R + t * spread), skyTintLin, groundTintLin, lightPropagationDir,
        lightColor, sunIntensity, roughness, enableAtmoSky, atmoSkyViewLut);
    vec3 tap1 = previewEnvCubemapRadiance(normalize(R - t * spread), skyTintLin, groundTintLin, lightPropagationDir,
        lightColor, sunIntensity, roughness, enableAtmoSky, atmoSkyViewLut);
    vec3 tap2 = previewEnvCubemapRadiance(normalize(R + b * spread), skyTintLin, groundTintLin, lightPropagationDir,
        lightColor, sunIntensity, roughness, enableAtmoSky, atmoSkyViewLut);
    vec3 tap3 = previewEnvCubemapRadiance(normalize(R - b * spread), skyTintLin, groundTintLin, lightPropagationDir,
        lightColor, sunIntensity, roughness, enableAtmoSky, atmoSkyViewLut);
    vec3 wide = (tap0 + tap1 + tap2 + tap3) * 0.25;

    vec3 probe = previewAmbientProbeIrradiance(N, skyTintLin, groundTintLin, lightPropagationDir, lightColor,
        sunIntensity, enableAtmoSky, atmoSkyViewLut);
    vec3 prefiltered = mix(center, wide, smoothstep(0.02, 0.35, roughness));
    return mix(prefiltered, probe, smoothstep(0.35, 1.0, roughness) * 0.55);
}

vec3 fakeIblSpecular(vec3 N, vec3 V, vec3 f0, float roughness, float metallic, vec3 skyTint, vec3 groundTint,
    vec3 lightPropagationDir, vec3 lightColor, float sunIntensity, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    vec3 R = reflect(-V, N);
    float NoV = max(dot(N, V), 0.0);

    // Dielectrics (cow hide, stone, etc.) need a roughness floor so IBL never mirror-paints the sky.
    float specRough = max(roughness, mix(0.72, 0.04, metallic));
    vec2 envBrdf = iblEnvBrdfFactor(NoV, specRough);

    vec3 prefiltered = iblPrefilteredSkyRadiance(N, R, specRough, skyTintLin, groundTintLin, lightPropagationDir,
        lightColor, sunIntensity, enableAtmoSky, atmoSkyViewLut);

    vec3 specular = prefiltered * (f0 * envBrdf.x + envBrdf.y);
    // Dielectrics: keep a faint glint; metals: full environment specular.
    float dielectricWt = (1.0 - metallic) * 0.08;
    float metalWt = metallic;
    return specular * (dielectricWt + metalWt);
}

#endif // GENESIS_IBL_GLSL
