// Genesis preview shader - directional shadow sampling helpers.
// Compatible with desktop GLSL 330 core and GLSL ES 300 (after GlslSourceAdapter).
// Hardware shadow comparison via sampler2DShadow + 3x3 PCF (9 taps), slope-scaled bias,
// and a manual border check (ES 300 has no CLAMP_TO_BORDER).

#ifndef GENESIS_SHADOW_GLSL
#define GENESIS_SHADOW_GLSL

//!include "common.glsl"

// Project a world-space position into shadow texture space and remap [-1,1] -> [0,1].
// Returns the shadow UV+depth in xyz, and writes 1.0 to outInside if the sample falls inside
// the [0,1]^3 light frustum cube (0.0 if outside, signaling "treat as fully lit").
vec3 worldToShadowUv(vec3 worldPos, mat4 lightVP, out float outInside)
{
    vec4 clip = lightVP * vec4(worldPos, 1.0);
    if (clip.w <= 0.0)
    {
        outInside = 0.0;
        return vec3(0.0);
    }

    vec3 ndc = clip.xyz / clip.w;
    vec3 uv = ndc * 0.5 + 0.5;

    float inside = 1.0;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || uv.z < 0.0 || uv.z > 1.0)
    {
        inside = 0.0;
    }

    outInside = inside;
    return uv;
}

// Slope-scaled bias: shadows acne grows with grazing angle, so scale the bias by (1 - N.L)
// and clamp into [minBias, maxBias].
float computeShadowBias(vec3 N, vec3 L, float minBias, float maxBias)
{
    float ndl = clamp(dot(normalize(N), normalize(L)), 0.0, 1.0);
    float slope = 1.0 - ndl;
    return clamp(maxBias * slope, minBias, maxBias);
}

// 3x3 PCF (9 taps) using hardware comparison. shadowUv.xy is the UV, .z is the receiver depth
// already bias-adjusted by the caller. texelSize = 1 / shadowMapResolution.
// PHASE3-CSM hook: cascade selection happens in the caller (e.g. by computing each cascade's
// shadowUv and picking the tightest non-clipped one); this helper is intentionally per-cascade
// and stateless.
float sampleShadowPcf3x3(sampler2DShadow shadowTex, vec3 shadowUv, vec2 texelSize)
{
    float sum = 0.0;
    // 3x3 grid: center + 8 neighbors.
    for (int dy = -1; dy <= 1; ++dy)
    {
        for (int dx = -1; dx <= 1; ++dx)
        {
            vec2 off = vec2(float(dx), float(dy)) * texelSize;
            vec3 p = vec3(shadowUv.xy + off, shadowUv.z);
            sum += texture(shadowTex, p);
        }
    }

    return sum * (1.0 / 9.0);
}

// PHASE3-CSM: cascade selection hook
// When cascades arrive, replace the single uLightViewProj with an array (e.g. uLightViewProj[4])
// and an array of sampler2DShadow (or a sampler2DArrayShadow). The caller selects the cascade by
// view-space depth, computes shadowUv with that cascade's matrix, and then calls
// sampleShadowPcf3x3 with the matching shadow map handle. The current single-cascade path is
// the degenerate case where N == 1.

#endif // GENESIS_SHADOW_GLSL
