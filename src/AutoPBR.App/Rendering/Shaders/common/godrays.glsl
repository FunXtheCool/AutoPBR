#ifndef GENESIS_GODRAYS_GLSL
#define GENESIS_GODRAYS_GLSL

//!include "common/shadow.glsl"

// Cheap Henyey-Greenstein forward lobe (g ~ 0.62) for sun-aligned scattering.
float godRayPhase(float cosTheta)
{
    const float g = 0.62;
    float gg = g * g;
    float denom = pow(max(1.0 + gg - 2.0 * g * cosTheta, 1e-3), 1.5);
    return (1.0 - gg) / (4.0 * 3.14159265358979323846 * denom);
}

float godRayParticipatingDensity(vec3 worldPos, float densityScale)
{
    // Ground-hugging aerosol proxy; preview stage sits near y = 0.
    float h = max(worldPos.y - 0.02, 0.0);
    return densityScale * exp(-h * 0.38);
}

float godRayShadowVisibility(vec3 samplePos, vec3 L, mat4 lightVP, sampler2DShadow shadowMap,
                             vec2 shadowTexelSize, float shadowMinBias, float shadowMaxBias, int enableShadowMap)
{
    if (enableShadowMap < 1)
    {
        return 1.0;
    }

    vec4 shadowPack = worldToShadowUv(samplePos, lightVP);
    if (shadowPack.w < 0.5)
    {
        return 1.0;
    }

    vec3 sUv = shadowPack.xyz;
    vec3 upN = vec3(0.0, 1.0, 0.0);
    float bias = computeShadowBias(upN, L, shadowMinBias, shadowMaxBias);
    sUv.z = clamp(sUv.z - bias, 0.0, 1.0);
    return sampleShadowPcf3x3(shadowMap, sUv, shadowTexelSize);
}

// Integrate in-scattering along the camera -> fragment segment (primary light = -lightPropagDir).
#define GODRAY_VIEW_MARCH_STEPS 10

vec3 integrateGodRaysAlongView(vec3 worldPos, vec3 cameraPos, vec3 lightPropagDir, vec3 lightColor,
                               mat4 lightVP, sampler2DShadow shadowMap, vec2 shadowTexelSize,
                               float shadowMinBias, float shadowMaxBias, int enableShadowMap,
                               float strength, float densityScale)
{
    vec3 toFrag = worldPos - cameraPos;
    float rayLen = length(toFrag);
    if (rayLen < 1e-3 || strength <= 0.0 || densityScale <= 0.0)
    {
        return vec3(0.0);
    }

    vec3 rayDir = toFrag / rayLen;
    vec3 L = normalize(-lightPropagDir);
    float phase = godRayPhase(dot(rayDir, L));

    float stepLen = rayLen / float(GODRAY_VIEW_MARCH_STEPS);
    vec3 scatter = vec3(0.0);
    float transmittance = 1.0;
    const float extinction = 0.72;

    for (int i = 0; i < GODRAY_VIEW_MARCH_STEPS; ++i)
    {
        float t = (float(i) + 0.5) * stepLen;
        vec3 samplePos = cameraPos + rayDir * t;
        float localDensity = godRayParticipatingDensity(samplePos, densityScale);
        if (localDensity <= 1e-5)
        {
            continue;
        }

        float lightVis = godRayShadowVisibility(samplePos, L, lightVP, shadowMap, shadowTexelSize,
                                                shadowMinBias, shadowMaxBias, enableShadowMap);
        float segmentOptDepth = extinction * localDensity * stepLen;
        float segmentTrans = exp(-segmentOptDepth);
        vec3 inScatter = lightColor * localDensity * lightVis * phase * (1.0 - segmentTrans);
        scatter += transmittance * inScatter;
        transmittance *= segmentTrans;
        if (transmittance < 0.02)
        {
            break;
        }
    }

    return scatter * strength;
}

#endif // GENESIS_GODRAYS_GLSL
