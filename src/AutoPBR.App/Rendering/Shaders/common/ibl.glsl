// Genesis preview shader - environment IBL without a cubemap (2D sky-view LUT + hemisphere fallback).
// Diffuse: low-frequency hemisphere irradiance (N.y only - avoids per-quad sky stickers on block models).
// Specular: Epic split-sum with a dielectric roughness floor and attenuation (metals stay glossy).

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

// GI-probe style irradiance. A diffuse probe shifts ambient *energy* with only a gentle
// hue cast; it must not repaint albedo with saturated zenith blue. We average hemisphere
// anchors, fold in ground bounce, then temper chroma toward luminance.
vec3 iblSkyIrradiance(vec3 N, sampler2D atmoSkyViewLut, vec3 groundTintLin)
{
    vec3 skyZenith = sampleAtmoSkyViewRadianceLinear(vec3(0.0, 1.0, 0.0), atmoSkyViewLut);
    vec3 skyHorizon = sampleAtmoSkyViewRadianceLinear(normalize(vec3(0.15, 0.2, 1.0)), atmoSkyViewLut);
    float hemi = pow(clamp(N.y * 0.5 + 0.5, 0.0, 1.0), 0.72);
    vec3 skyIrr = mix(skyHorizon, skyZenith, hemi);

    // Ground bounce keeps downward faces lit (real GI from the grass/ground plane).
    vec3 groundRad = groundTintLin * 0.45;
    float skyOcc = smoothstep(-0.55, 0.62, N.y);
    vec3 irr = mix(groundRad, skyIrr, skyOcc);

    // Probe tempering: keep the brightness, retain only ~30 percent of the color cast.
    float lum = dot(irr, vec3(0.2126, 0.7152, 0.0722));
    return mix(vec3(lum), irr, 0.3);
}

vec3 fakeIblAmbientDiffuse(vec3 N, vec3 skyTint, vec3 groundTint, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    if (enableAtmoSky > 0)
    {
        return iblSkyIrradiance(N, atmoSkyViewLut, groundTintLin);
    }

    return fakeIblSky(N, skyTintLin, groundTintLin);
}

// Roughness-dependent environment radiance (stand-in for prefiltered cubemap mips).
vec3 iblPrefilteredSkyRadiance(vec3 N, vec3 R, float roughness, sampler2D atmoSkyViewLut)
{
    float alpha = brdfGgxAlpha(roughness);

    vec3 skyUp = sampleAtmoSkyViewRadianceLinear(vec3(0.0, 1.0, 0.0), atmoSkyViewLut);
    vec3 skyHor = sampleAtmoSkyViewRadianceLinear(normalize(vec3(0.2, 0.18, 1.0)), atmoSkyViewLut);
    vec3 hemiAvg = mix(skyHor, skyUp, 0.55);
    vec3 diffuseLike = mix(hemiAvg, iblSkyIrradiance(N, atmoSkyViewLut, vec3(0.0)), 0.5);

    vec3 up = abs(R.y) < 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 t = normalize(cross(up, R));
    vec3 b = cross(R, t);
    float spread = max(alpha * 2.8, 0.35);
    vec3 tap0 = sampleAtmoSkyViewRadianceLinear(normalize(R + t * spread), atmoSkyViewLut);
    vec3 tap1 = sampleAtmoSkyViewRadianceLinear(normalize(R - t * spread), atmoSkyViewLut);
    vec3 tap2 = sampleAtmoSkyViewRadianceLinear(normalize(R + b * spread), atmoSkyViewLut);
    vec3 tap3 = sampleAtmoSkyViewRadianceLinear(normalize(R - b * spread), atmoSkyViewLut);
    vec3 wide = (tap0 + tap1 + tap2 + tap3) * 0.25;

    float blurT = smoothstep(0.02, 0.98, roughness);
    return mix(wide, diffuseLike, blurT);
}

vec3 iblPrefilteredSkyRadianceFallback(vec3 N, vec3 R, float roughness, vec3 skyTintLin, vec3 groundTintLin)
{
    vec3 hemi = fakeIblSky(N, skyTintLin, groundTintLin);
    vec3 blur = mix(skyTintLin, groundTintLin, 0.5);
    float blurT = smoothstep(0.02, 0.98, roughness);
    return mix(blur, hemi, blurT);
}

// Split-sum specular IBL. Sun / punctual highlights come from direct lighting only.
vec3 fakeIblSpecular(vec3 N, vec3 V, vec3 f0, float roughness, float metallic, vec3 skyTint, vec3 groundTint,
    int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    vec3 R = reflect(-V, N);
    float NoV = max(dot(N, V), 0.0);

    // Dielectrics (cow hide, stone, etc.) need a roughness floor so IBL never mirror-paints the sky.
    float specRough = max(roughness, mix(0.72, 0.04, metallic));
    vec2 envBrdf = iblEnvBrdfFactor(NoV, specRough);

    vec3 prefiltered;
    if (enableAtmoSky > 0)
    {
        prefiltered = iblPrefilteredSkyRadiance(N, R, specRough, atmoSkyViewLut);
        float below = smoothstep(0.1, -0.28, R.y);
        vec3 groundRef = groundTintLin * (0.04 + 0.07 * specRough);
        prefiltered = mix(prefiltered, groundRef, below);
    }
    else
    {
        prefiltered = iblPrefilteredSkyRadianceFallback(N, R, specRough, skyTintLin, groundTintLin);
    }

    vec3 specular = prefiltered * (f0 * envBrdf.x + envBrdf.y);
    // Dielectrics: keep a faint glint; metals: full environment specular.
    float dielectricWt = (1.0 - metallic) * 0.08;
    float metalWt = metallic;
    return specular * (dielectricWt + metalWt);
}

#endif // GENESIS_IBL_GLSL
