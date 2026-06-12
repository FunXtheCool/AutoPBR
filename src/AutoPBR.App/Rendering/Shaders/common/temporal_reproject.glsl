// Shared screen-space temporal helpers for preview volumetrics and final TAA.
// Per-effect histories stay separate; this file owns reprojection math only.

#ifndef GENESIS_TEMPORAL_REPROJECT_GLSL
#define GENESIS_TEMPORAL_REPROJECT_GLSL

//!include "ray_reconstruct.glsl"

// Reproject a known world-space point (cloud slab anchor, froxel sample, etc.).
vec2 trReprojectUvFromWorld(vec3 worldPos, mat4 prevViewProj)
{
    vec4 prevClip = prevViewProj * vec4(worldPos, 1.0);
    if (prevClip.w <= 1e-6)
    {
        return vec2(-1.0);
    }

    vec2 prevNdc = prevClip.xy / prevClip.w;
    return prevNdc * 0.5 + 0.5;
}

// Reproject a screen UV + hardware depth into previous-frame UV space.
vec2 trReprojectUvFromDepth(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
{
    vec3 worldPos = grWorldPosFromUvDepth(uv, depth, invViewProj);
    return trReprojectUvFromWorld(worldPos, prevViewProj);
}

bool trPrevUvOnScreen(vec2 prevUv)
{
    return prevUv.x >= 0.0 && prevUv.x <= 1.0 && prevUv.y >= 0.0 && prevUv.y <= 1.0;
}

// Fade history near the previous frame's texture borders to avoid clamp smear.
float trHistoryBorderWeight(vec2 prevUv, float fadeWidth)
{
    if (!trPrevUvOnScreen(prevUv))
    {
        return 0.0;
    }

    vec2 borderDist = min(prevUv, vec2(1.0) - prevUv);
    return smoothstep(0.0, fadeWidth, min(borderDist.x, borderDist.y));
}

// Reject history when scene depth disagrees (disocclusion).
float trDepthDisocclusionWeight(float currentDepth, float historyDepth, float edge0, float edge1)
{
    return 1.0 - smoothstep(edge0, edge1, abs(historyDepth - currentDepth));
}

// 3x3 neighborhood min/max on the current frame (TAA-style variance clamp input).
void trNeighborhoodMinMax3(sampler2D currentTex, vec2 uv, vec2 texelSize, out vec3 nMin, out vec3 nMax)
{
    nMin = vec3(1e6);
    nMax = vec3(-1e6);
    for (int oy = -1; oy <= 1; ++oy)
    {
        for (int ox = -1; ox <= 1; ++ox)
        {
            vec2 tapUv = clamp(uv + vec2(float(ox), float(oy)) * texelSize, vec2(0.001), vec2(0.999));
            vec3 tap = texture(currentTex, tapUv).rgb;
            nMin = min(nMin, tap);
            nMax = max(nMax, tap);
        }
    }
}

vec3 trClampHistoryToNeighborhood(vec3 history, vec3 nMin, vec3 nMax)
{
    return clamp(history, nMin, nMax);
}

// Screen-space velocity from depth reprojection (current UV minus previous UV).
vec2 trScreenVelocityFromDepth(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
{
    vec2 prevUv = trReprojectUvFromDepth(uv, depth, invViewProj, prevViewProj);
    if (!trPrevUvOnScreen(prevUv))
    {
        return vec2(0.0);
    }

    return uv - prevUv;
}

// Reject history when camera/object motion is large (disocclusion / new pixels).
float trMotionRejectionWeight(vec2 velocity, float edge0, float edge1)
{
    return 1.0 - smoothstep(edge0, edge1, length(velocity));
}

#endif // GENESIS_TEMPORAL_REPROJECT_GLSL
