// Shadow gates for froxel inject (no volumetric_medium include).

#ifndef GENESIS_SHADOW_GATES_GLSL
#define GENESIS_SHADOW_GATES_GLSL

//!include "shadow.glsl"

float grShadowGate(vec3 worldPos, mat4 lightViewProj, sampler2DShadow shadowMap, vec2 shadowTexelSize,
    float shadowMinBias, int enableShadowMap)
{
    if (enableShadowMap < 1)
    {
        return 1.0;
    }

    vec4 shadowPack = worldToShadowUv(worldPos, lightViewProj);
    if (shadowPack.w < 0.5)
    {
        return 1.0;
    }

    vec3 shadowUv = shadowPack.xyz;
    float texel = shadowMapTexelDepth(shadowTexelSize);
    float bias = max(shadowMinBias, texel * 1.75);
    shadowUv.z = clamp(shadowUv.z - bias, 0.0, 1.0);
    return sampleShadowPcf3x3(shadowMap, shadowUv, shadowTexelSize);
}

float grShadowGateCascaded(vec3 worldPos, vec3 cameraPos, mat4 lightViewProjNear, mat4 lightViewProjFar,
    sampler2DShadow shadowNear, sampler2DShadow shadowFar, vec2 shadowTexelSize, float shadowMinBias,
    int enableShadowMap, int enableCascades, float cascadeSplitDistance)
{
    if (enableShadowMap < 1)
    {
        return 1.0;
    }

    if (enableCascades < 1)
    {
        return grShadowGate(worldPos, lightViewProjFar, shadowFar, shadowTexelSize, shadowMinBias, enableShadowMap);
    }

    float dist = length(worldPos - cameraPos);
    if (dist < cascadeSplitDistance)
    {
        return grShadowGate(worldPos, lightViewProjNear, shadowNear, shadowTexelSize, shadowMinBias, enableShadowMap);
    }

    return grShadowGate(worldPos, lightViewProjFar, shadowFar, shadowTexelSize, shadowMinBias, enableShadowMap);
}

#endif // GENESIS_SHADOW_GATES_GLSL
