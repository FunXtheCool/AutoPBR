#version 330 core
// AutoPBR Genesis preview shader - fragment stage.
// Algorithms inspired by LabPBR 1.3 spec (https://shaderlabs.org/wiki/LabPBR_Material_Standard)
// and Glimmer Shaders (MIT, https://github.com/jbritain/glimmer-shaders).
// Single-pass forward emulation of POM, parallax self-shadow, Cook-Torrance,
// LabPBR-aware metal/dielectric F0, subsurface scattering, environment IBL,
// emission, directional shadow map (Phase 2) and ACES Filmic tonemapping.

//!include "common/common.glsl"
//!include "common/material_labpbr.glsl"
//!include "common/brdf.glsl"
//!include "common/parallax.glsl"
//!include "common/sss.glsl"
//!include "common/ibl.glsl"
//!include "common/tonemap.glsl"
//!include "common/shadow.glsl"

in vec3 vWorldPos;
in vec3 vWorldNormal;
in vec2 vUv;
in vec4 vWorldTangent;
in vec4 vLightClip;

uniform sampler2D uAlbedo;
uniform sampler2D uNormal;
uniform sampler2D uSpecular;
uniform sampler2D uHeight;
uniform sampler2D uAtmoSkyViewLut;
uniform sampler2DShadow uShadowMap;

uniform vec3  uCameraPos;
uniform vec3  uLightDir;
uniform vec3  uLightColor;
uniform float uAmbient;
uniform float uNormalStrength;
uniform float uHeightStrength;
uniform float uSpecularStrength;
uniform float uRoughnessScale;
uniform float uExposure;
uniform float uParallaxAoStrength;
uniform int   uEnableParallax;
uniform int   uEnableParallaxAo;
uniform int   uEnableNormalMap;
uniform int   uEnableSpecularMap;
uniform int   uHasNormal;
uniform int   uHasSpecular;
uniform int   uHasHeight;
uniform int   uSceneKind;
uniform float uAlphaCutoff;
uniform int   uItemAlphaBlend;
// 0 = off, 1 = cutout, 2 = blend - only applied for batched previews that opt in (entity emulated rigs).
uniform int   uEntityAlphaMode;

// Genesis-specific.
uniform int   uEnableSss;
uniform int   uEnableParallaxShadow;
uniform int   uEnableIbl;
uniform int   uEnableAtmosphericSky;
uniform float uSssStrength;
uniform float uIblStrength;
uniform float uEmissionStrength;
uniform vec3  uSkyTint;
uniform vec3  uGroundTint;
uniform float uAtmosphereTurbidity;
uniform float uAtmosphereSunIntensity;
uniform float uAtmosphereHorizonFalloff;
uniform float uAerialFogStrength;

// Genesis directional shadow map (Phase 2).
uniform mat4  uLightViewProj;
uniform int   uEnableShadowMap;
uniform float uShadowMinBias;
uniform float uShadowMaxBias;
uniform vec2  uShadowTexelSize;

out vec4 FragColor;

vec3 sampleNormal(vec2 uv, vec3 Nw, vec3 Tw, vec3 Bw)
{
    if (uEnableNormalMap < 1 || uHasNormal < 1)
    {
        return normalize(Nw);
    }

    vec3 tn = texture(uNormal, uv).xyz * 2.0 - 1.0;
    tn.xy *= uNormalStrength;
    tn = normalize(tn);
    mat3 tbn = mat3(normalize(Tw), normalize(Bw), normalize(Nw));
    return normalize(tbn * tn);
}

void main()
{
    vec4 albRaw = texture(uAlbedo, vUv);
    if (uSceneKind == 1)
    {
        if (uItemAlphaBlend < 1 && albRaw.a < uAlphaCutoff)
        {
            discard;
        }
    }

    // Build TBN in world space.
    vec3 Nw = normalize(vWorldNormal);
    vec3 Tw = normalize(vWorldTangent.xyz);
    Tw = normalize(Tw - dot(Tw, Nw) * Nw);
    vec3 Bw = cross(Nw, Tw) * vWorldTangent.w;
    mat3 worldToTan = transpose(mat3(Tw, Bw, Nw));

    vec3 Vw = normalize(uCameraPos - vWorldPos);
    vec3 Vtan = normalize(worldToTan * Vw);

    // Parallax occlusion mapping in tangent space.
    vec2 uvDisp = vUv;
    vec2 uv = vUv;
    float pomDepth = 0.0;
    float pomStrengthTrace = uHeightStrength;
    bool  pomActive = (uEnableParallax > 0 && uHasHeight > 0 && uHeightStrength > 0.0);
    if (pomActive)
    {
        // Fade POM near UV seams so adjacent cube faces do not fight each other's tangent march at corners.
        float pomBorderDist = min(min(vUv.x, 1.0 - vUv.x), min(vUv.y, 1.0 - vUv.y));
        pomStrengthTrace *= smoothstep(0.0, 0.09, pomBorderDist);

        uvDisp = traceParallaxPom(uHeight, vUv, Vtan, pomStrengthTrace, pomDepth);
        // Clamp for sampling only. Mixing back toward vUv when OOB caused visible swimming as the camera moved.
        uv = clamp(uvDisp, vec2(0.0), vec2(1.0));
    }

    // Re-sample albedo at displaced UV when POM is on.
    vec4 alb = pomActive ? texture(uAlbedo, uv) : albRaw;
    if (uEntityAlphaMode == 1 && alb.a < uAlphaCutoff)
    {
        discard;
    }

    vec3 albedoLinear = srgbToLinear(alb.rgb);

    // Surface normal.
    vec3 N = sampleNormal(uv, Nw, Tw, Bw);
    vec3 V = Vw;
    vec3 L = normalize(-uLightDir); // uLightDir points where the light goes; flip for incoming direction.
    vec3 Ltan = normalize(worldToTan * L);

    // LabPBR _s decode (or neutral defaults when no spec map / spec map disabled).
    LabPbrMaterial mat;
    if (uEnableSpecularMap > 0 && uHasSpecular > 0)
    {
        vec4 sp = texture(uSpecular, uv);
        mat = decodeLabPbrSpec(sp, albedoLinear, uRoughnessScale, uSpecularStrength);
    }
    else
    {
        mat.smoothness = 0.0;
        mat.roughness = 0.9;
        mat.f0 = vec3(0.04);
        mat.metallic = 0.0;
        mat.sssAmount = 0.0;
        mat.porosity = 0.0;
        mat.emissionStrength = 0.0;
    }

    // Porosity cosmetic darkening (only visible when LabPBR _s.b <= 64).
    albedoLinear *= porosityAlbedoMultiplier(mat.porosity);

    // Cheap cavity/contrast boost from POM depth (screen shaders often combine AO + POM for perceived thickness).
    if (pomActive && pomDepth > 1e-4)
    {
        float cavityAmt = clamp(pomDepth * pomStrengthTrace * 1.68, 0.0, 0.34);
        albedoLinear *= (1.0 - cavityAmt);
    }

    // Parallax self-shadow trace toward the light (fragment-local; complements the directional shadow map).
    float pomShadow = 1.0;
    float pomAo = 1.0;
    if (pomActive && uEnableParallaxShadow > 0 && uHasHeight > 0)
    {
        pomShadow = traceParallaxShadow(uHeight, uv, Ltan, pomDepth, pomStrengthTrace);
        if (uEnableParallaxAo > 0)
        {
            pomAo = traceParallaxAo(uHeight, uv, pomDepth, pomStrengthTrace, uParallaxAoStrength);
        }
    }

    // Directional shadow map visibility (Phase 2).
    float shadowVis = 1.0;
    if (uEnableShadowMap > 0)
    {
        // Use the precomputed light-clip varying so this stays cheap; POM displacement is
        // intentionally ignored in shadowing (matches Iris/LabPBR conventions; future phases
        // can promote pomDepth into world-space and refine).
        if (vLightClip.w > 0.0)
        {
            vec3 ndc = vLightClip.xyz / vLightClip.w;
            vec3 sUv = ndc * 0.5 + 0.5;
            // Manual border check (ES 300 has no CLAMP_TO_BORDER; CLAMP_TO_EDGE would otherwise
            // return the fragment as shadowed when sampled beyond [0,1]).
            if (sUv.x >= 0.0 && sUv.x <= 1.0 && sUv.y >= 0.0 && sUv.y <= 1.0 &&
                sUv.z >= 0.0 && sUv.z <= 1.0)
            {
                float bias = computeShadowBias(N, L, uShadowMinBias, uShadowMaxBias);
                sUv.z = clamp(sUv.z - bias, 0.0, 1.0);
                shadowVis = sampleShadowPcf3x3(uShadowMap, sUv, uShadowTexelSize);
            }
        }
    }

    // Combined lighting visibility: parallax-local AND directional shadow gate.
    float lightVis = pomShadow * shadowVis;

    // Direct lighting: Cook-Torrance.
    BrdfResult br = cookTorrance(N, V, L, albedoLinear, mat.f0, mat.roughness, mat.metallic);
    vec3 direct = (br.diffuse + br.specular) * uLightColor * lightVis;

    // Subsurface scattering contribution (gated; cheap front-wrap + back-translucency).
    if (uEnableSss > 0 && mat.sssAmount > 0.0)
    {
        float sssScale = uSssStrength * lightVis;
        direct += sssWrappedDiffuse(N, L, albedoLinear, mat.sssAmount, uLightColor) * sssScale;
        direct += sssTransmission(V, L, albedoLinear, mat.sssAmount, uLightColor) * sssScale;
    }

    // Indirect lighting: environment IBL (LUT + sun when atmospheric sky is on).
    vec3 indirect = vec3(0.0);
    if (uEnableIbl > 0)
    {
        vec3 iblDiff = fakeIblAmbientDiffuse(N, uSkyTint, uGroundTint, uEnableAtmosphericSky, uAtmoSkyViewLut);
        // Only dielectric (1 - metallic) gets diffuse indirect; metals are reflection-only.
        indirect += iblDiff * albedoLinear * (1.0 - mat.metallic) * uIblStrength;
        indirect += fakeIblSpecular(N, V, mat.f0, mat.roughness, uSkyTint, uGroundTint, uEnableAtmosphericSky,
                           uAtmoSkyViewLut, uLightDir, uLightColor, uAtmosphereSunIntensity)
                       * uIblStrength;
    }
    else
    {
        // Constant ambient fallback so previews are not pitch-black with IBL off.
        indirect = albedoLinear * uAmbient * (1.0 - mat.metallic);
    }

    // Parallax contact AO mostly darkens indirect/cavity light while keeping direct lobe readable.
    indirect *= pomAo;
    direct *= mix(1.0, pomAo, 0.22);

    // Emission (LabPBR _s.a additive).
    vec3 emission = albedoLinear * mat.emissionStrength * uEmissionStrength;

    vec3 hdr = (direct + indirect + emission) * uExposure;
    vec3 mapped = tonemapAcesNarkowicz(hdr);

    if (uAerialFogStrength > 0.0 && uEnableAtmosphericSky > 0)
    {
        float dist = length(vWorldPos - uCameraPos);
        float fogAmt = (1.0 - exp(-dist * 0.042 * uAerialFogStrength)) * 0.65;
        vec3 fogCol = mix(uGroundTint, uSkyTint, 0.55);
        mapped = mix(mapped, fogCol, fogAmt);
    }

    vec3 srgb = linearToSrgb(mapped);

    float a = 1.0;
    if (uSceneKind == 1 && uItemAlphaBlend > 0)
    {
        a = alb.a;
    }
    else if (uEntityAlphaMode == 2)
    {
        a = alb.a;
    }

    FragColor = vec4(srgb, a);
}
