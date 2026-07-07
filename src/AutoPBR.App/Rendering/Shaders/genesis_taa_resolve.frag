#version 330 core
// Final preview TAA: stabilizes composited RGB on geometry and immediate geometry silhouettes.
// Sky / screen-space volumetrics use far-plane depth in the capture buffer and only participate at silhouettes.

//!include "common/common.glsl"
//!include "common/temporal_reproject.glsl"

in vec2 vUv;
uniform sampler2D uCurrent;
uniform sampler2D uHistory;
uniform sampler2D uSceneDepth;
uniform sampler2D uTaaSignal;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
uniform vec2 uTexelSize;
uniform vec2 uCurrentJitterPixels;
uniform float uTemporalWeight;
uniform float uStableTemporalBoost;
uniform float uMaxStableTemporal;
uniform float uTaaSharpenStrength;
uniform float uDepthEdgeHistoryFloor;
uniform float uEdgeAaBlend;
uniform float uSourceFilterStrength;
uniform float uSilhouetteHistoryWeight;
uniform float uFxaaEdgeStrength;
uniform float uFxaaLumaEdgeStrength;
uniform float uFxaaLumaThreshold;
uniform int uHasSceneDepth;
uniform int uHasTaaSignal;
uniform int uHasHistory;
uniform int uForceFxaa;

out vec4 FragColor;

const float SKY_DEPTH_EPS = 0.9992;

float taaMitchell(float x)
{
    x = abs(x);
    float x2 = x * x;
    float x3 = x2 * x;
    if (x < 1.0)
    {
        return ((7.0 / 6.0) * x3) - (2.0 * x2) + (8.0 / 9.0);
    }

    if (x < 2.0)
    {
        return ((-7.0 / 18.0) * x3) + (2.0 * x2) - ((10.0 / 3.0) * x) + (16.0 / 9.0);
    }

    return 0.0;
}

vec3 taaCurrentResolveFilter(sampler2D tex, vec2 uv, vec2 texelSize, vec2 jitterPixels)
{
    vec3 sum = vec3(0.0);
    float weightSum = 0.0;
    for (int oy = -1; oy <= 1; ++oy)
    {
        for (int ox = -1; ox <= 1; ++ox)
        {
            vec2 pixelOffset = vec2(float(ox), float(oy));
            float w = taaMitchell(length(pixelOffset + jitterPixels));
            vec2 tapUv = clamp(uv + pixelOffset * texelSize, vec2(0.001), vec2(0.999));
            sum += texture(tex, tapUv).rgb * w;
            weightSum += w;
        }
    }

    return clamp(sum / max(weightSum, 1e-5), vec3(0.0), vec3(1.0));
}

float taaClosestGeometryDepth3(sampler2D depthTex, vec2 uv, vec2 texelSize)
{
    float closestDepth = 1.0;
    for (int oy = -1; oy <= 1; ++oy)
    {
        for (int ox = -1; ox <= 1; ++ox)
        {
            vec2 tapUv = clamp(uv + vec2(float(ox), float(oy)) * texelSize, vec2(0.001), vec2(0.999));
            closestDepth = min(closestDepth, texture(depthTex, tapUv).r);
        }
    }

    return closestDepth;
}

float taaLumaEdgeMask(sampler2D tex, vec2 uv, vec2 texelSize)
{
    vec3 rgbM = texture(tex, uv).rgb;
    vec3 rgbN = texture(tex, clamp(uv + vec2( 0.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbS = texture(tex, clamp(uv + vec2( 0.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbW = texture(tex, clamp(uv + vec2(-1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbE = texture(tex, clamp(uv + vec2( 1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;

    float lumaM = trLuminance(rgbM);
    float lumaN = trLuminance(rgbN);
    float lumaS = trLuminance(rgbS);
    float lumaW = trLuminance(rgbW);
    float lumaE = trLuminance(rgbE);
    float range = max(max(abs(lumaN - lumaS), abs(lumaW - lumaE)),
        max(max(abs(lumaM - lumaN), abs(lumaM - lumaS)), max(abs(lumaM - lumaW), abs(lumaM - lumaE))));
    float thresholdLow = clamp(uFxaaLumaThreshold, 0.001, 0.12);
    float thresholdHigh = max(thresholdLow + 0.02, thresholdLow * 7.75);
    return smoothstep(thresholdLow, thresholdHigh, range);
}

vec3 taaFxaaLite(sampler2D tex, vec2 uv, vec2 texelSize, vec3 resolved, float edgeMask, float strength)
{
    if (strength <= 0.0 || edgeMask <= 0.0)
    {
        return resolved;
    }

    vec3 rgbNW = texture(tex, clamp(uv + vec2(-1.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbNE = texture(tex, clamp(uv + vec2( 1.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbSW = texture(tex, clamp(uv + vec2(-1.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbSE = texture(tex, clamp(uv + vec2( 1.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbM = texture(tex, uv).rgb;

    float lumaNW = trLuminance(rgbNW);
    float lumaNE = trLuminance(rgbNE);
    float lumaSW = trLuminance(rgbSW);
    float lumaSE = trLuminance(rgbSE);
    float lumaM = trLuminance(rgbM);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    vec2 dir = vec2(
        -((lumaNW + lumaNE) - (lumaSW + lumaSE)),
         ((lumaNW + lumaSW) - (lumaNE + lumaSE)));
    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.0078125, 0.0078125);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = clamp(dir * rcpDirMin, vec2(-4.0), vec2(4.0)) * texelSize;

    vec3 rgbA = 0.5 * (
        texture(tex, clamp(uv + dir * (1.0 / 3.0 - 0.5), vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + dir * (2.0 / 3.0 - 0.5), vec2(0.001), vec2(0.999))).rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        texture(tex, clamp(uv + dir * -0.5, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + dir *  0.5, vec2(0.001), vec2(0.999))).rgb);
    float lumaB = trLuminance(rgbB);
    vec3 fxaa = (lumaB < lumaMin || lumaB > lumaMax) ? rgbA : rgbB;
    float mixW = clamp(edgeMask * strength, 0.0, 0.75);
    return mix(resolved, fxaa, mixW);
}

vec3 taaTentBlur3x3(sampler2D tex, vec2 uv, vec2 texelSize)
{
    vec3 center = texture(tex, uv).rgb * 4.0;
    vec3 cardinals =
        texture(tex, clamp(uv + vec2(-1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2( 1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2( 0.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2( 0.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 diagonals =
        texture(tex, clamp(uv + vec2(-1.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2( 1.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2(-1.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb +
        texture(tex, clamp(uv + vec2( 1.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    return (center + cardinals * 2.0 + diagonals) * (1.0 / 16.0);
}

vec3 taaMorphologicalEdgeBlend(
    sampler2D colorTex,
    sampler2D depthTex,
    vec2 uv,
    vec2 texelSize,
    vec3 resolved,
    float depthEdgeAmount,
    float nearbyGeometryW,
    float lumaEdgeMask,
    float fxaaEdgeMask,
    float strength,
    int hasDepth,
    int forceFullFrame)
{
    if (strength <= 0.0)
    {
        return resolved;
    }

    vec3 rgbM = texture(colorTex, uv).rgb;
    vec3 rgbN = texture(colorTex, clamp(uv + vec2( 0.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbS = texture(colorTex, clamp(uv + vec2( 0.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbW = texture(colorTex, clamp(uv + vec2(-1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;
    vec3 rgbE = texture(colorTex, clamp(uv + vec2( 1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).rgb;

    float lumaM = trLuminance(rgbM);
    float lumaN = trLuminance(rgbN);
    float lumaS = trLuminance(rgbS);
    float lumaW = trLuminance(rgbW);
    float lumaE = trLuminance(rgbE);
    vec2 lumaGrad = vec2(lumaE - lumaW, lumaS - lumaN);
    vec2 depthGrad = vec2(0.0);
    if (hasDepth > 0)
    {
        float dN = texture(depthTex, clamp(uv + vec2( 0.0, -1.0) * texelSize, vec2(0.001), vec2(0.999))).r;
        float dS = texture(depthTex, clamp(uv + vec2( 0.0,  1.0) * texelSize, vec2(0.001), vec2(0.999))).r;
        float dW = texture(depthTex, clamp(uv + vec2(-1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).r;
        float dE = texture(depthTex, clamp(uv + vec2( 1.0,  0.0) * texelSize, vec2(0.001), vec2(0.999))).r;
        depthGrad = vec2(dE - dW, dS - dN) * 8.0;
    }

    vec2 grad = lumaGrad * clamp(uFxaaLumaEdgeStrength, 0.0, 2.0) + depthGrad;
    float gradLen = length(grad);
    vec2 normal = gradLen > 1e-5 ? grad / gradLen : vec2(0.70710678, 0.70710678);
    float edgeMask = forceFullFrame > 0
        ? 1.0
        : max(fxaaEdgeMask, max(lumaEdgeMask, depthEdgeAmount * nearbyGeometryW));
    if (edgeMask <= 0.0)
    {
        return resolved;
    }

    vec3 nearA = texture(colorTex, clamp(uv + normal * texelSize * 0.70, vec2(0.001), vec2(0.999))).rgb;
    vec3 nearB = texture(colorTex, clamp(uv - normal * texelSize * 0.70, vec2(0.001), vec2(0.999))).rgb;
    vec3 farA = texture(colorTex, clamp(uv + normal * texelSize * 1.35, vec2(0.001), vec2(0.999))).rgb;
    vec3 farB = texture(colorTex, clamp(uv - normal * texelSize * 1.35, vec2(0.001), vec2(0.999))).rgb;
    vec3 acrossNormal = mix((nearA + nearB) * 0.5, (farA + farB) * 0.5, 0.35);
    vec3 cardinal = (rgbN + rgbS + rgbW + rgbE) * 0.25;

    float colorContrast = max(max(abs(lumaM - lumaN), abs(lumaM - lumaS)),
        max(abs(lumaM - lumaW), abs(lumaM - lumaE)));
    float normalContrast = abs(trLuminance(nearA) - trLuminance(nearB));
    float colorBoost = smoothstep(0.010, 0.180, max(colorContrast, normalContrast));
    float depthBoost = hasDepth > 0 ? smoothstep(0.025, 0.350, depthEdgeAmount) * nearbyGeometryW : 0.0;
    float blendW = forceFullFrame > 0
        ? clamp(strength * 0.62, 0.0, 0.94)
        : clamp(edgeMask * strength * (0.42 + 0.50 * max(colorBoost, depthBoost)), 0.0, 0.90);
    vec3 target = mix(acrossNormal, cardinal, forceFullFrame > 0 ? 0.16 : 0.06);
    return mix(resolved, target, blendW);
}

void main()
{
    vec3 currentPoint = texture(uCurrent, vUv).rgb;
    float depth = uHasSceneDepth > 0 ? texture(uSceneDepth, vUv).r : 1.0;
    float rawDepthEdgeW = 1.0;
    float closestDepth = depth;
    if (uHasSceneDepth > 0)
    {
        rawDepthEdgeW = trDepthEdgeWeight(uSceneDepth, vUv, uTexelSize);
        closestDepth = taaClosestGeometryDepth3(uSceneDepth, vUv, uTexelSize);
    }

    float depthEdgeAmount = 1.0 - rawDepthEdgeW;
    float depthGeometryW = uHasSceneDepth > 0 ? 1.0 - step(SKY_DEPTH_EPS, depth) : 1.0;
    float nearbyGeometryW = 1.0 - step(SKY_DEPTH_EPS, closestDepth);
    float silhouetteW = depthEdgeAmount * nearbyGeometryW * clamp(uSilhouetteHistoryWeight, 0.0, 1.0);
    float sourceFilterW = clamp(uSourceFilterStrength, 0.0, 1.0) *
        (uHasSceneDepth > 0 ? depthEdgeAmount : 0.15);
    vec3 currentFiltered = taaCurrentResolveFilter(uCurrent, vUv, uTexelSize, uCurrentJitterPixels);
    vec3 current = mix(currentPoint, currentFiltered, clamp(sourceFilterW, 0.0, 0.80));
    bool isSky = uHasSceneDepth > 0 && depth >= SKY_DEPTH_EPS;
    vec4 signal = uHasTaaSignal > 0 ? texture(uTaaSignal, vUv) : vec4(0.0);
    float reactivity = clamp(signal.r, 0.0, 1.0);
    float objectMotion = clamp(signal.b, 0.0, 1.0);
    float signalGeometryW = uHasTaaSignal > 0 ? step(0.5, signal.g) : 0.0;
    float geometryW = max(depthGeometryW, signalGeometryW);
    float coverageW = max(geometryW, silhouetteW);
    vec3 resolved = current;

    if (coverageW > 0.0 && uHasHistory > 0 && uTemporalWeight > 0.0)
    {
        float edgeReprojectionW = uHasSceneDepth > 0 ? smoothstep(0.12, 0.85, depthEdgeAmount) : 0.0;
        float reprojectionDepth = mix(depth, closestDepth, edgeReprojectionW);
        vec2 prevUv = trReprojectUvFromDepth(vUv, reprojectionDepth, uInvViewProj, uPrevViewProj);
        if (trPrevUvOnScreen(prevUv))
        {
            float borderW = trHistoryBorderWeight(prevUv, 0.04);
            float depthW = 1.0;
            float depthEdgeW = 1.0;
            if (uHasSceneDepth > 0)
            {
                depthW = trDepthDisocclusionWeight(reprojectionDepth, texture(uSceneDepth, prevUv).r, 0.002, 0.02);
                depthEdgeW = max(max(rawDepthEdgeW, clamp(uDepthEdgeHistoryFloor, 0.0, 1.0)), silhouetteW);
            }

            vec2 velocity = vUv - prevUv;
            float motionW = trMotionRejectionWeight(velocity,
                uHasSceneDepth > 0 ? 0.0015 : 0.002,
                uHasSceneDepth > 0 ? 0.035 : 0.05);

            vec3 nMin;
            vec3 nMax;
            trNeighborhoodMinMax3YCoCg(uCurrent, vUv, uTexelSize, nMin, nMax);
            vec3 history = texture(uHistory, prevUv).rgb;
            history = trClipHistoryToNeighborhoodYCoCg(history, current, nMin, nMax);
            float reactiveW = trLuminanceReactiveWeight(current, history);
            float signalW = (1.0 - smoothstep(0.18, 0.90, reactivity)) *
                (1.0 - smoothstep(0.08, 0.75, objectMotion));
            float stableMask = borderW * depthW * depthEdgeW * motionW * reactiveW * signalW * coverageW;
            float stillW = (1.0 - smoothstep(0.0015, 0.012, length(velocity))) *
                (1.0 - smoothstep(0.02, 0.28, objectMotion)) *
                (1.0 - smoothstep(0.05, 0.45, reactivity)) *
                depthEdgeW;
            float stableTemporal = min(uMaxStableTemporal, uTemporalWeight + uStableTemporalBoost);
            float blend = mix(uTemporalWeight, stableTemporal, stillW) * stableMask;
            resolved = mix(current, history, blend);

            vec3 blur = (
                texture(uCurrent, vUv + vec2(-1.0, 0.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 1.0, 0.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 0.0,-1.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 0.0, 1.0) * uTexelSize).rgb) * 0.25;
            float stableW = blend * (1.0 - reactivity) * (1.0 - objectMotion) *
                (1.0 - smoothstep(0.08, 0.32, length(current - history)));
            resolved += (resolved - blur) * (uTaaSharpenStrength * stableW);

            if (uHasSceneDepth > 0 && uEdgeAaBlend > 0.0)
            {
                float edgeAmount = depthEdgeAmount * depthW * coverageW *
                    (1.0 - smoothstep(0.05, 0.45, reactivity)) *
                    (1.0 - smoothstep(0.08, 0.75, objectMotion));
                resolved = mix(resolved, blur, clamp(edgeAmount * uEdgeAaBlend, 0.0, 0.55));
            }
        }
    }

    if (coverageW > 0.0 && uHasHistory > 0 && uTemporalWeight > 0.0 && uEdgeAaBlend > 0.0)
    {
        vec3 edgeMin;
        vec3 edgeMax;
        trNeighborhoodMinMax3YCoCg(uCurrent, vUv, uTexelSize, edgeMin, edgeMax);
        vec3 edgeHistory = texture(uHistory, vUv).rgb;
        edgeHistory = trClipHistoryToNeighborhoodYCoCg(edgeHistory, resolved, edgeMin, edgeMax);
        float colorEdgeAmount = taaLumaEdgeMask(uCurrent, vUv, uTexelSize);
        float edgeHistoryW = (depthEdgeAmount * nearbyGeometryW + colorEdgeAmount * 0.35) *
            clamp(uEdgeAaBlend, 0.0, 1.0) *
            clamp(uTemporalWeight, 0.0, 0.98) *
            (1.0 - smoothstep(0.16, 0.85, reactivity)) *
            (1.0 - smoothstep(0.10, 0.80, objectMotion));
        resolved = mix(resolved, edgeHistory, clamp(edgeHistoryW, 0.0, 0.55));
    }

    float depthFxaaMask = uHasSceneDepth > 0
        ? smoothstep(0.04, 0.55, depthEdgeAmount) * nearbyGeometryW
        : 0.0;
    float lumaFxaaMask = uForceFxaa > 0 ? 1.0 : taaLumaEdgeMask(uCurrent, vUv, uTexelSize);
    float geometryFxaaW = uForceFxaa > 0 ? 1.0 : (uHasTaaSignal > 0 ? max(geometryW, nearbyGeometryW) : 1.0);
    float fxaaEdgeMask = uForceFxaa > 0
        ? 1.0
        : max(depthFxaaMask, lumaFxaaMask * geometryFxaaW * clamp(uFxaaLumaEdgeStrength, 0.0, 2.0));
    resolved = taaFxaaLite(uCurrent, vUv, uTexelSize, resolved, fxaaEdgeMask, uFxaaEdgeStrength);
    resolved = taaMorphologicalEdgeBlend(
        uCurrent,
        uSceneDepth,
        vUv,
        uTexelSize,
        resolved,
        depthEdgeAmount,
        nearbyGeometryW,
        lumaFxaaMask,
        fxaaEdgeMask,
        uFxaaEdgeStrength,
        uHasSceneDepth,
        uForceFxaa);
    float postFxaaW = uForceFxaa > 0
        ? clamp(uFxaaEdgeStrength * 0.34, 0.0, 0.90)
        : clamp(fxaaEdgeMask * uFxaaEdgeStrength * 0.28, 0.0, 0.55);
    resolved = mix(resolved, taaTentBlur3x3(uCurrent, vUv, uTexelSize), postFxaaW);

    FragColor = vec4(resolved, 1.0);
}
