#version 330 core
// Full-res bilateral upsample of half-res god rays + temporal reprojection (P1).

//!include "common/common.glsl"

in vec2 vUv;
uniform sampler2D uHalfResRays;
uniform sampler2D uSceneDepth;
uniform sampler2D uHistory;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
uniform vec2 uHalfResTexelSize;
uniform float uTemporalWeight;
uniform int uHasHistory;

out vec4 FragColor;

vec3 bilateralUpsample(vec2 uv, float centerDepth)
{
    vec3 accum = vec3(0.0);
    float wSum = 0.0;

    for (int oy = 0; oy <= 1; ++oy)
    {
        for (int ox = 0; ox <= 1; ++ox)
        {
            vec2 offset = vec2(float(ox) - 0.5, float(oy) - 0.5) * uHalfResTexelSize;
            vec2 tapUv = clamp(uv + offset, vec2(0.001), vec2(0.999));
            float tapDepth = texture(uSceneDepth, tapUv).r;
            float depthW = exp(-abs(tapDepth - centerDepth) * 1400.0);
            vec3 tapRays = texture(uHalfResRays, tapUv).rgb;
            accum += tapRays * depthW;
            wSum += depthW;
        }
    }

    return accum / max(wSum, 1e-4);
}

vec2 reprojectUv(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    float z = depth * 2.0 - 1.0;
    vec4 worldH = invViewProj * vec4(ndc, z, 1.0);
    vec3 worldPos = worldH.xyz / max(worldH.w, 1e-6);
    vec4 prevClip = prevViewProj * vec4(worldPos, 1.0);
    if (prevClip.w <= 1e-6)
    {
        return vec2(-1.0);
    }

    vec2 prevNdc = prevClip.xy / prevClip.w;
    return prevNdc * 0.5 + 0.5;
}

void main()
{
    float depth = texture(uSceneDepth, vUv).r;
    vec3 current = bilateralUpsample(vUv, depth);
    vec3 finalRays = current;

    if (uHasHistory > 0)
    {
        vec2 prevUv = reprojectUv(vUv, depth, uInvViewProj, uPrevViewProj);
        if (prevUv.x >= 0.0 && prevUv.x <= 1.0 && prevUv.y >= 0.0 && prevUv.y <= 1.0)
        {
            vec3 history = texture(uHistory, prevUv).rgb;
            float histDepth = texture(uSceneDepth, prevUv).r;
            float depthValid = 1.0 - smoothstep(0.002, 0.02, abs(histDepth - depth));
            float blend = uTemporalWeight * depthValid;
            finalRays = mix(current, history, blend);
        }
    }

    FragColor = vec4(finalRays, 1.0);
}
