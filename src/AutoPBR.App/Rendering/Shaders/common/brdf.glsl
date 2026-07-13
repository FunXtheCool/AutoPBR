// Genesis preview shader - Cook-Torrance BRDF terms.
// D: GGX/Trowbridge-Reitz. V: Smith correlated GGX. F: Schlick (with roughness-aware variant for IBL).
// Diffuse: Lambert (albedo / PI). Energy split kD = (1 - F) * (1 - metallic).

#ifndef GENESIS_BRDF_GLSL
#define GENESIS_BRDF_GLSL

//!include "common.glsl"

// LabPBR roughness is already (1 - smoothness)^2; use it directly as GGX alpha.
float brdfGgxAlpha(float roughness)
{
    return clamp(roughness, 0.002, 1.0);
}

float D_GGX(float NoH, float alpha)
{
    float a2 = alpha * alpha;
    float d  = (NoH * NoH) * (a2 - 1.0) + 1.0;
    return a2 / max(GEN_PI * d * d, GEN_EPS);
}

// Heitz / Frostbite correlated Smith visibility (already divides by 4*NoL*NoV).
float V_SmithGGXCorrelated(float NoV, float NoL, float alpha)
{
    float a2 = alpha * alpha;
    float lambdaV = NoL * sqrt(NoV * NoV * (1.0 - a2) + a2);
    float lambdaL = NoV * sqrt(NoL * NoL * (1.0 - a2) + a2);
    return 0.5 / max(lambdaV + lambdaL, GEN_EPS);
}

vec3 F_Schlick(float VoH, vec3 f0)
{
    return f0 + (vec3(1.0) - f0) * pow5(1.0 - saturate1(VoH));
}

// Epic split-sum DFG approximation (Lazarov / UE4 mobile notes).
// Specular IBL = prefilteredEnv * (f0 * scale + bias). Do not multiply by F again.
vec2 iblEnvBrdfFactor(float NoV, float roughness)
{
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572, 0.022);
    const vec4 c1 = vec4(1.0, 0.0425, 1.04, -0.04);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    return vec2(-1.04, 1.04) * a004 + r.zw;
}

struct BrdfResult
{
    vec3 diffuse;   // (kD * albedo / PI) * NoL
    vec3 specular;  // D*V*F * NoL
};

BrdfResult cookTorrance(vec3 N, vec3 V, vec3 L, vec3 albedo, vec3 f0, float roughness, float metallic)
{
    BrdfResult r;
    vec3 H = normalize(V + L);
    float NoV = max(dot(N, V), GEN_EPS);
    float NoL = max(dot(N, L), 0.0);
    float NoH = max(dot(N, H), 0.0);
    float VoH = max(dot(V, H), 0.0);

    float alpha = brdfGgxAlpha(roughness);

    float D = D_GGX(NoH, alpha);
    float V_ = V_SmithGGXCorrelated(NoV, max(NoL, GEN_EPS), alpha);
    vec3  F = F_Schlick(VoH, f0);

    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metallic);

    r.diffuse  = kD * albedo * GEN_INV_PI * NoL;
    r.specular = D * V_ * F * NoL;
    return r;
}

#endif // GENESIS_BRDF_GLSL
