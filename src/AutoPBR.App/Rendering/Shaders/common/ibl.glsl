// Genesis preview shader - environment IBL without a cubemap (2D sky-view LUT + hemisphere fallback).
// Without atmospheric sky: simple procedural sky/ground gradient.
// With atmospheric sky: diffuse/specular sample the LUT; specular adds an analytic reflected sun
// (skylight scatter only is baked into the LUT).

#ifndef GENESIS_IBL_GLSL
#define GENESIS_IBL_GLSL

//!include "common.glsl"
//!include "brdf.glsl"
//!include "atmosphere.glsl"

vec3 fakeIblSky(vec3 dir, vec3 skyTint, vec3 groundTint)
{
    float t = clamp(dir.y * 0.5 + 0.5, 0.0, 1.0);
    return mix(groundTint, skyTint, smoothstep(0.0, 1.0, t));
}

// Inverse of atmo_skyview.frag direction reconstruction:
//   sinTheta = sin(vUv.y * PI); dir ~ normalize(vec3(sinTheta * (vUv.x*2-1), cos(vUv.y*PI), sinTheta))
vec2 atmoSkyViewUvFromWorldDir(vec3 dirRaw)
{
    vec3 d = normalize(dirRaw);
    float vy = 0.5;
    if (length(vec2(d.y, d.z)) > 1e-6)
    {
        vy = atan(d.z, d.y) / ATM_PI;
    }

    vy = clamp(vy, 0.0, 1.0);

    float vx = 0.5;
    if (abs(d.z) > 1e-5)
    {
        vx = clamp(d.x / d.z * 0.5 + 0.5, 0.0, 1.0);
    }
    else if (abs(d.x) > 1e-5)
    {
        vx = clamp(sign(d.x) * 0.49 + 0.5, 0.0, 1.0);
    }

    return vec2(vx, vy);
}

vec3 sampleAtmoSkyViewRadianceLinear(vec3 dirWorld, sampler2D atmoSkyViewLut)
{
    vec2 uv = atmoSkyViewUvFromWorldDir(dirWorld);
    return srgbToLinear(texture(atmoSkyViewLut, uv).rgb);
}

vec3 fakeIblAmbientDiffuse(vec3 N, vec3 skyTint, vec3 groundTint, int enableAtmoSky, sampler2D atmoSkyViewLut)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    if (enableAtmoSky > 0)
    {
        vec3 skyRad = sampleAtmoSkyViewRadianceLinear(N, atmoSkyViewLut);
        float skyOcc = smoothstep(-0.38, 0.72, N.y);
        vec3 groundRad = groundTintLin * 0.07;
        return mix(groundRad, skyRad, skyOcc);
    }

    return fakeIblSky(N, skyTintLin, groundTintLin);
}

vec3 fakeIblSpecular(vec3 N, vec3 V, vec3 f0, float roughness, vec3 skyTint, vec3 groundTint, int enableAtmoSky,
    sampler2D atmoSkyViewLut, vec3 sunPropagationDir, vec3 punctualLightColor, float atmosphereSunIntensity)
{
    vec3 skyTintLin = srgbToLinear(skyTint);
    vec3 groundTintLin = srgbToLinear(groundTint);
    vec3 R = reflect(-V, N);
    float NoV = max(dot(N, V), 0.0);
    vec3 F = F_SchlickRoughness(NoV, f0, roughness);

    if (enableAtmoSky > 0)
    {
        vec3 skyR = sampleAtmoSkyViewRadianceLinear(R, atmoSkyViewLut);
        vec3 skyUp = sampleAtmoSkyViewRadianceLinear(vec3(0.0, 1.0, 0.0), atmoSkyViewLut);
        vec3 skyLow = sampleAtmoSkyViewRadianceLinear(normalize(vec3(0.15, 0.25, 1.0)), atmoSkyViewLut);
        float mipBlur = clamp(roughness, 0.0, 1.0);
        vec3 envWide = mix(skyUp, skyLow, 0.6);
        vec3 envRad = mix(skyR, envWide, mipBlur);

        float below = smoothstep(0.12, -0.28, R.y);
        vec3 groundRef = groundTintLin * (0.035 + 0.09 * roughness);
        envRad = mix(envRad, groundRef, below);

        vec3 Lsun = normalize(-sunPropagationDir);
        float cosAlpha = max(dot(normalize(R), Lsun), 0.0);
        float sunSharp = mix(900.0, 14000.0, 1.0 - roughness);
        float sunDisk = pow(cosAlpha, sunSharp);
        vec3 sunRad = punctualLightColor * atmosphereSunColor(atmosphereSunIntensity);
        vec3 sunReflected = sunRad * sunDisk * mix(0.012, 0.095, 1.0 - roughness);

        return (envRad + sunReflected) * F;
    }

    vec3 envSharp = fakeIblSky(R, skyTintLin, groundTintLin);
    vec3 envBlur = mix(skyTintLin, groundTintLin, 0.5);
    vec3 env = mix(envSharp, envBlur, clamp(roughness, 0.0, 1.0));
    return env * F;
}

#endif // GENESIS_IBL_GLSL
