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

vec3 trRgbToYCoCg(vec3 c)
{
    return vec3(
        dot(c, vec3(0.25, 0.5, 0.25)),
        c.r - c.b,
        c.g - 0.5 * (c.r + c.b));
}

vec3 trYCoCgToRgb(vec3 c)
{
    float t = c.x - 0.5 * c.z;
    return vec3(t + 0.5 * c.y, c.x + 0.5 * c.z, t - 0.5 * c.y);
}

void trNeighborhoodMinMax3YCoCg(sampler2D currentTex, vec2 uv, vec2 texelSize, out vec3 nMin, out vec3 nMax)
{
    nMin = vec3(1e6);
    nMax = vec3(-1e6);
    for (int oy = -1; oy <= 1; ++oy)
    {
        for (int ox = -1; ox <= 1; ++ox)
        {
            vec2 tapUv = clamp(uv + vec2(float(ox), float(oy)) * texelSize, vec2(0.001), vec2(0.999));
            vec3 tap = trRgbToYCoCg(texture(currentTex, tapUv).rgb);
            nMin = min(nMin, tap);
            nMax = max(nMax, tap);
        }
    }
}

void trNeighborhoodMinMax3YCoCgFromTaps(vec3 taps[9], out vec3 nMin, out vec3 nMax)
{
    nMin = vec3(1e6);
    nMax = vec3(-1e6);
    for (int i = 0; i < 9; ++i)
    {
        vec3 tap = trRgbToYCoCg(taps[i]);
        nMin = min(nMin, tap);
        nMax = max(nMax, tap);
    }
}

vec3 trClipHistoryToNeighborhoodYCoCg(vec3 historyRgb, vec3 currentRgb, vec3 nMin, vec3 nMax)
{
    vec3 history = trRgbToYCoCg(historyRgb);
    vec3 current = trRgbToYCoCg(currentRgb);
    vec3 center = (nMin + nMax) * 0.5;
    vec3 extent = max((nMax - nMin) * 0.5, vec3(1e-5));
    vec3 offset = history - center;
    vec3 unit = abs(offset) / extent;
    float maxUnit = max(max(unit.x, unit.y), unit.z);
    vec3 clipped = maxUnit > 1.0 ? center + offset / maxUnit : history;
    clipped = mix(clipped, clamp(history, nMin, nMax), 0.25);
    return maxUnit > 3.0 ? currentRgb : clamp(trYCoCgToRgb(clipped), vec3(0.0), vec3(64.0));
}

float trLuminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

float trLuminanceReactiveWeight(vec3 current, vec3 history)
{
    float lc = trLuminance(current);
    float lh = trLuminance(history);
    float diff = abs(lc - lh) / max(max(lc, lh), 0.04);
    return 1.0 - smoothstep(0.08, 0.35, diff);
}

float trDepthEdgeWeight(sampler2D depthTex, vec2 uv, vec2 texelSize)
{
    float dMin = 1.0;
    float dMax = 0.0;
    for (int oy = -1; oy <= 1; ++oy)
    {
        for (int ox = -1; ox <= 1; ++ox)
        {
            vec2 tapUv = clamp(uv + vec2(float(ox), float(oy)) * texelSize, vec2(0.001), vec2(0.999));
            float d = texture(depthTex, tapUv).r;
            dMin = min(dMin, d);
            dMax = max(dMax, d);
        }
    }

    return 1.0 - smoothstep(0.0025, 0.018, dMax - dMin);
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
