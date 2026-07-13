// Genesis preview shader - LabPBR 1.3 specular decoder.
// Reference: https://shaderlabs.org/wiki/LabPBR_Material_Standard
// Predefined-metal F0 RGB triplets are derived from spec n,k at normal incidence.

#ifndef GENESIS_MATERIAL_LABPBR_GLSL
#define GENESIS_MATERIAL_LABPBR_GLSL

//!include "common.glsl"

struct LabPbrMaterial
{
    float smoothness;        // _s.r in 0..1
    float roughness;         // (1 - smoothness)^2
    vec3  f0;                // Fresnel at normal incidence (linear RGB)
    float metallic;          // 0 dielectric, 1 metal
    float sssAmount;         // _s.b >= 65 -> (b - 65) / 190 in 0..1
    float porosity;          // _s.b <= 64 -> b / 64 in 0..1
    float emissionStrength;  // _s.a < 1.0 -> (254 - a*255) / 254 in 0..1
};

// Predefined-metal F0 RGB lookup for LabPBR g indices 230..237.
const vec3 LABPBR_METAL_F0[8] = vec3[8](
    vec3(0.531229, 0.512357, 0.495829), // Iron 230
    vec3(0.944230, 0.776102, 0.373402), // Gold 231
    vec3(0.912298, 0.913851, 0.919681), // Aluminum 232
    vec3(0.555597, 0.554537, 0.554779), // Chrome 233
    vec3(0.925952, 0.720902, 0.504154), // Copper 234
    vec3(0.632484, 0.625937, 0.641479), // Lead 235
    vec3(0.678849, 0.642401, 0.588410), // Platinum 236
    vec3(0.962000, 0.949468, 0.922116)  // Silver 237
);

vec3 labPbrPredefinedMetalF0(int gIndex, vec3 albedo)
{
    if (gIndex >= 230 && gIndex <= 237)
    {
        return LABPBR_METAL_F0[gIndex - 230];
    }

    return albedo;
}

LabPbrMaterial decodeLabPbrSpec(vec4 specSample, vec3 albedoLinear, float roughnessScale, float smoothnessScale)
{
    LabPbrMaterial m;

    float smoothness = saturate1(specSample.r * smoothnessScale);
    m.smoothness = smoothness;
    float baseRough = (1.0 - smoothness) * (1.0 - smoothness);
    m.roughness = clamp(baseRough * roughnessScale, 0.002, 1.0);

    // G channel: 0..229 dielectric F0, 230..237 predefined metal, 238..254 reserved metal, 255 custom metal.
    float gByte = specSample.g * 255.0;
    int   gIdx  = int(gByte + 0.5);
    if (gIdx <= 229)
    {
        float f0Scalar = clamp(gByte / 255.0, 0.0, 0.9);
        m.f0 = vec3(f0Scalar);
        m.metallic = 0.0;
    }
    else if (gIdx == 255)
    {
        m.f0 = albedoLinear;
        m.metallic = 1.0;
    }
    else
    {
        m.f0 = labPbrPredefinedMetalF0(gIdx, albedoLinear);
        m.metallic = 1.0;
    }

    // B channel: 0..64 porosity, 65..255 subsurface scattering amount.
    float bByte = specSample.b * 255.0;
    if (bByte <= 64.0)
    {
        m.porosity = saturate1(bByte / 64.0);
        m.sssAmount = 0.0;
    }
    else
    {
        m.porosity = 0.0;
        m.sssAmount = saturate1((bByte - 65.0) / 190.0);
    }

    // A channel: 255 (1.0) means no emission. a < 1 means emissive: strength = (254 - a*255) / 254.
    float aByte = specSample.a * 255.0;
    if (aByte >= 254.5)
    {
        m.emissionStrength = 0.0;
    }
    else
    {
        m.emissionStrength = saturate1((254.0 - aByte) / 254.0);
    }

    return m;
}

#endif // GENESIS_MATERIAL_LABPBR_GLSL
