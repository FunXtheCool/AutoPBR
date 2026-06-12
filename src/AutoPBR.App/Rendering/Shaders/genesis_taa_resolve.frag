#version 330 core
// Final preview TAA: stabilizes composited RGB on geometry only.
// Sky / screen-space volumetrics use far-plane depth in the capture buffer and must not accumulate here.

//!include "common/common.glsl"
//!include "common/temporal_reproject.glsl"

in vec2 vUv;
uniform sampler2D uCurrent;
uniform sampler2D uHistory;
uniform sampler2D uSceneDepth;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
uniform vec2 uTexelSize;
uniform float uTemporalWeight;
uniform int uHasSceneDepth;
uniform int uHasHistory;

out vec4 FragColor;

const float SKY_DEPTH_EPS = 0.9992;

void main()
{
    vec3 current = texture(uCurrent, vUv).rgb;
    float depth = uHasSceneDepth > 0 ? texture(uSceneDepth, vUv).r : 1.0;
    bool isSky = uHasSceneDepth > 0 && depth >= SKY_DEPTH_EPS;
    vec3 resolved = current;

    if (!isSky && uHasHistory > 0 && uTemporalWeight > 0.0)
    {
        vec2 prevUv = trReprojectUvFromDepth(vUv, depth, uInvViewProj, uPrevViewProj);
        if (trPrevUvOnScreen(prevUv))
        {
            float borderW = trHistoryBorderWeight(prevUv, 0.04);
            float depthW = 1.0;
            if (uHasSceneDepth > 0)
            {
                depthW = trDepthDisocclusionWeight(depth, texture(uSceneDepth, prevUv).r, 0.002, 0.02);
            }

            vec2 velocity = vUv - prevUv;
            float motionW = trMotionRejectionWeight(velocity,
                uHasSceneDepth > 0 ? 0.0015 : 0.002,
                uHasSceneDepth > 0 ? 0.035 : 0.05);

            vec3 nMin;
            vec3 nMax;
            trNeighborhoodMinMax3(uCurrent, vUv, uTexelSize, nMin, nMax);
            vec3 history = texture(uHistory, prevUv).rgb;
            history = trClampHistoryToNeighborhood(history, nMin, nMax);
            float blend = uTemporalWeight * borderW * depthW * motionW;
            resolved = mix(current, history, blend);
        }
    }

    FragColor = vec4(resolved, 1.0);
}
