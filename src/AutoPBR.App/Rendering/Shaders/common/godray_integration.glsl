// God-ray integration helpers: world reconstruction, cloud attenuation, shadow gates.

#ifndef GENESIS_GODRAY_INTEGRATION_GLSL
#define GENESIS_GODRAY_INTEGRATION_GLSL

//!include "shadow.glsl"
//!include "volumetric_medium.glsl"

vec3 grWorldPosFromUvDepth(vec2 uv, float depth, mat4 invViewProj)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    float z = depth * 2.0 - 1.0;
    vec4 worldH = invViewProj * vec4(ndc, z, 1.0);
    return worldH.xyz / max(worldH.w, 1e-6);
}

vec3 grWorldRayDir(vec2 uv, mat4 invViewProj, vec3 cameraPos)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    vec4 worldH = invViewProj * vec4(ndc, 1.0, 1.0);
    vec3 farPt = worldH.xyz / max(worldH.w, 1e-6);
    vec3 rd = farPt - cameraPos;
    float len2 = dot(rd, rd);
    if (len2 < 1e-12)
    {
        return vec3(0.0, 1.0, 0.0);
    }
    return rd * inversesqrt(len2);
}

vec3 grMarchWorldPos(vec2 uv, float sampleDepth, mat4 invViewProj, vec3 cameraPos, float layerBase, float layerTop)
{
    if (sampleDepth > 0.9995 || sampleDepth < 1e-5)
    {
        vec3 rd = grWorldRayDir(uv, invViewProj, cameraPos);
        float midY = (layerBase + layerTop) * 0.5;
        float t = 40.0;
        if (abs(rd.y) > 1e-4)
        {
            t = (midY - cameraPos.y) / rd.y;
        }
        if (t < 1.0)
        {
            t = 40.0;
        }
        return cameraPos + rd * t;
    }

    return grWorldPosFromUvDepth(uv, sampleDepth, invViewProj);
}

float grCloudAttenuation(vec3 worldPos, float groundWorldY, float fogSlabTopY, float layerBase, float layerTop,
    float cloudDensityMul, float volumeSize, float heightFogStrength, int enableClouds)
{
    if (enableClouds < 1)
    {
        return 1.0;
    }

    float density = vmMediumDensity(worldPos, groundWorldY, fogSlabTopY, layerBase, layerTop,
        cloudDensityMul, volumeSize, heightFogStrength);
    return vmMediumTransmittance(density, 3.4);
}

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
    int enableShadowMap, int enableCascades, float cascadeSplitDistance, float cascadeBlendWidth)
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
    float halfBand = max(cascadeBlendWidth, 0.0) * 0.5;
    float blendStart = cascadeSplitDistance - halfBand;
    float blendEnd = cascadeSplitDistance + halfBand;

    if (halfBand <= 1e-5 || dist <= blendStart)
    {
        return grShadowGate(worldPos, lightViewProjNear, shadowNear, shadowTexelSize, shadowMinBias, enableShadowMap);
    }

    if (dist >= blendEnd)
    {
        return grShadowGate(worldPos, lightViewProjFar, shadowFar, shadowTexelSize, shadowMinBias, enableShadowMap);
    }

    float nearVis = grShadowGate(worldPos, lightViewProjNear, shadowNear, shadowTexelSize, shadowMinBias, enableShadowMap);
    float farVis = grShadowGate(worldPos, lightViewProjFar, shadowFar, shadowTexelSize, shadowMinBias, enableShadowMap);
    float blendT = smoothstep(blendStart, blendEnd, dist);
    return mix(nearVis, farVis, blendT);
}

#endif // GENESIS_GODRAY_INTEGRATION_GLSL
