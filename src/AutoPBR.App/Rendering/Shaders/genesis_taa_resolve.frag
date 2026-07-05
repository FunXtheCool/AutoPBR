#version 330 core
// Final preview TAA: stabilizes composited RGB on geometry only.
// Sky / screen-space volumetrics use far-plane depth in the capture buffer and must not accumulate here.

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
uniform float uTemporalWeight;
uniform int uHasSceneDepth;
uniform int uHasTaaSignal;
uniform int uHasHistory;

out vec4 FragColor;

const float SKY_DEPTH_EPS = 0.9992;

void main()
{
    vec3 current = texture(uCurrent, vUv).rgb;
    float depth = uHasSceneDepth > 0 ? texture(uSceneDepth, vUv).r : 1.0;
    bool isSky = uHasSceneDepth > 0 && depth >= SKY_DEPTH_EPS;
    vec4 signal = uHasTaaSignal > 0 ? texture(uTaaSignal, vUv) : vec4(0.0);
    float reactivity = clamp(signal.r, 0.0, 1.0);
    float objectMotion = clamp(signal.b, 0.0, 1.0);
    float geometryW = uHasTaaSignal > 0 ? step(0.5, signal.g) : (isSky ? 0.0 : 1.0);
    vec3 resolved = current;

    if (!isSky && geometryW > 0.0 && uHasHistory > 0 && uTemporalWeight > 0.0)
    {
        vec2 prevUv = trReprojectUvFromDepth(vUv, depth, uInvViewProj, uPrevViewProj);
        if (trPrevUvOnScreen(prevUv))
        {
            float borderW = trHistoryBorderWeight(prevUv, 0.04);
            float depthW = 1.0;
            float depthEdgeW = 1.0;
            if (uHasSceneDepth > 0)
            {
                depthW = trDepthDisocclusionWeight(depth, texture(uSceneDepth, prevUv).r, 0.002, 0.02);
                depthEdgeW = trDepthEdgeWeight(uSceneDepth, vUv, uTexelSize);
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
            float blend = uTemporalWeight * borderW * depthW * depthEdgeW * motionW * reactiveW * signalW;
            resolved = mix(current, history, blend);

            vec3 blur = (
                texture(uCurrent, vUv + vec2(-1.0, 0.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 1.0, 0.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 0.0,-1.0) * uTexelSize).rgb +
                texture(uCurrent, vUv + vec2( 0.0, 1.0) * uTexelSize).rgb) * 0.25;
            float stableW = blend * (1.0 - reactivity) * (1.0 - objectMotion) *
                (1.0 - smoothstep(0.08, 0.32, length(current - history)));
            resolved += (resolved - blur) * (0.08 * stableW);
        }
    }

    FragColor = vec4(resolved, 1.0);
}
