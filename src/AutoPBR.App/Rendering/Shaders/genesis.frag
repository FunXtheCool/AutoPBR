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
in vec4 vCurrClip;
in vec4 vPrevClip;

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
uniform int   uIsGroundPass;
uniform float uAlphaCutoff;
uniform int   uItemAlphaBlend;
// 0 = off, 1 = cutout, 2 = blend - batched block/item models and entity emulated rigs.
uniform int   uEntityAlphaMode;
uniform int   uPreviewDepthLayerDebug;
uniform vec3  uPreviewLayerDebugTint;

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
uniform float uShadowSoftnessTexels;

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 TaaSignal;

vec3 sampleNormal(vec2 uv, vec2 dx, vec2 dy, vec3 Nw, vec3 Tw, vec3 Bw)
{
    if (uEnableNormalMap < 1 || uHasNormal < 1)
    {
        return normalize(Nw);
    }

    vec3 tn = textureGrad(uNormal, uv, dx, dy).xyz * 2.0 - 1.0;
    tn.xy *= uNormalStrength;
    tn = normalize(tn);
    mat3 tbn = mat3(normalize(Tw), normalize(Bw), normalize(Nw));
    return normalize(tbn * tn);
}

float metalPreviewBaseVisibility(float roughness)
{
    return mix(0.22, 0.38, saturate1(roughness));
}

float groundSpecularReceiverFade(vec3 worldPos, vec3 N, vec3 V)
{
    if (uIsGroundPass < 1)
    {
        return 1.0;
    }

    float dist = length(worldPos - uCameraPos);
    float distFade = 1.0 - smoothstep(18.0, 48.0, dist);
    float noV = max(dot(normalize(N), normalize(V)), 0.0);
    float grazingFade = smoothstep(0.045, 0.22, noV);
    return clamp(mix(0.08, 1.0, distFade * grazingFade), 0.08, 1.0);
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
    else if (uEntityAlphaMode == 1 && albRaw.a < uAlphaCutoff)
    {
        discard;
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
    vec2 uvDx = dFdx(vUv);
    vec2 uvDy = dFdy(vUv);
    float pomDepth = 0.0;
    float pomStrengthTrace = uHeightStrength;
    bool  pomActive = (uEnableParallax > 0 && uHasHeight > 0 && uHeightStrength > 0.0);
    if (pomActive)
    {
        // Trace in tile-local space; height/albedo/normal/spec samples wrap so repeated ground tiles and cube-face edges stay seamless.
        uvDisp = traceParallaxPom(uHeight, vUv, Vtan, pomStrengthTrace, uvDx, uvDy, pomDepth);
        uv = uvDisp;
    }

    // Re-sample albedo at displaced UV when POM is on.
    vec4 alb = pomActive ? textureGrad(uAlbedo, uv, uvDx, uvDy) : albRaw;
    if (uEntityAlphaMode == 1 && alb.a < uAlphaCutoff)
    {
        discard;
    }

    vec3 albedoLinear = srgbToLinear(alb.rgb);
    if (uPreviewDepthLayerDebug != 0)
    {
        albedoLinear = uPreviewLayerDebugTint;
    }

    // Surface normal.
    vec3 N = sampleNormal(uv, uvDx, uvDy, Nw, Tw, Bw);
    vec3 V = Vw;
    vec3 L = normalize(-uLightDir); // uLightDir points where the light goes; flip for incoming direction.
    vec3 Ltan = normalize(worldToTan * L);

    // LabPBR _s decode (or neutral defaults when no spec map / spec map disabled).
    LabPbrMaterial mat;
    if (uEnableSpecularMap > 0 && uHasSpecular > 0)
    {
        vec4 sp = textureGrad(uSpecular, uv, uvDx, uvDy);
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

    // Parallax self-shadow trace toward the light (fragment-local; complements the directional shadow map).
    float pomShadow = 1.0;
    float pomAo = 1.0;
    if (pomActive && uEnableParallaxShadow > 0 && uHasHeight > 0)
    {
        pomShadow = traceParallaxShadow(uHeight, uv, Ltan, pomDepth, pomStrengthTrace, uvDx, uvDy);
        if (uEnableParallaxAo > 0)
        {
            pomAo = traceParallaxAo(uHeight, uv, pomDepth, pomStrengthTrace, uParallaxAoStrength, uvDx, uvDy);
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
                shadowVis = sampleShadowPcfSoft(uShadowMap, sUv, uShadowTexelSize, uShadowSoftnessTexels);
            }
        }
    }

    // Combined lighting visibility: parallax-local AND directional shadow gate.
    float lightVis = pomShadow * shadowVis;

    // Direct lighting: Cook-Torrance.
    BrdfResult br = cookTorrance(N, V, L, albedoLinear, mat.f0, mat.roughness, mat.metallic);
    float groundSpecFade = groundSpecularReceiverFade(vWorldPos, N, V);
    br.specular *= groundSpecFade;
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
        vec3 iblDiff = fakeIblAmbientDiffuse(N, uSkyTint, uGroundTint, uLightDir, uLightColor,
            uAtmosphereSunIntensity, uEnableAtmosphericSky, uAtmoSkyViewLut);
        float dielectricDiffuseVisibility = 1.0 - mat.metallic;
        // The preview has no captured scene cubemap. Keep a small irradiance-backed base
        // for LabPBR metal IDs so valid G>=230 pixels do not crush to black off-highlight.
        float metalBaseVisibility = mat.metallic * metalPreviewBaseVisibility(mat.roughness);
        vec3 metalPreviewIrradiance = max(iblDiff, vec3(uAmbient * 0.75));
        indirect += iblDiff * albedoLinear * dielectricDiffuseVisibility * uIblStrength;
        indirect += metalPreviewIrradiance * albedoLinear * metalBaseVisibility * uIblStrength;
        indirect += fakeIblSpecular(N, V, mat.f0, mat.roughness, mat.metallic, uSkyTint, uGroundTint,
                           uLightDir, uLightColor, uAtmosphereSunIntensity, uEnableAtmosphericSky, uAtmoSkyViewLut) *
                    uIblStrength * groundSpecFade;
    }
    else
    {
        // Constant ambient fallback so previews are not pitch-black with IBL off.
        float dielectricAmbientVisibility = 1.0 - mat.metallic;
        float metalAmbientVisibility = mat.metallic * metalPreviewBaseVisibility(mat.roughness);
        indirect = albedoLinear * uAmbient * (dielectricAmbientVisibility + metalAmbientVisibility);
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

    float alphaSpan = max(fwidth(alb.a), 1.0 / 255.0);
    float alphaEdge = 1.0 - smoothstep(1.0, 4.0, abs(alb.a - uAlphaCutoff) / alphaSpan);
    float reactivity = 0.0;
    reactivity = max(reactivity, uIsGroundPass > 0 ? 0.08 : 0.0);
    reactivity = max(reactivity, uSceneKind == 1 ? 0.25 : 0.0);
    reactivity = max(reactivity, uEntityAlphaMode == 1 ? mix(0.20, 0.72, alphaEdge) : 0.0);
    reactivity = max(reactivity, uEntityAlphaMode == 2 ? max(0.65, 1.0 - alb.a) : 0.0);

    FragColor = vec4(srgb, a);
    float motion = 0.0;
    if (vCurrClip.w > 1e-6 && vPrevClip.w > 1e-6)
    {
        vec2 currUv = (vCurrClip.xy / vCurrClip.w) * 0.5 + 0.5;
        vec2 prevUv = (vPrevClip.xy / vPrevClip.w) * 0.5 + 0.5;
        motion = clamp(length(currUv - prevUv) * 64.0, 0.0, 1.0);
    }

    TaaSignal = vec4(clamp(reactivity, 0.0, 1.0), 1.0, motion, 1.0);
}
