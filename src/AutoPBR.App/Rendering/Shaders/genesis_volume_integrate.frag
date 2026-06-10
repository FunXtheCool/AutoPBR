#version 330 core
// GENESIS_GLES_PACK rev29
// Full froxel integrate: view-ray Mie in-scatter march with froxel-space + screen-space temporal reuse,
// plus a cloud-output mode for the volumetric cloud composite.
// ANGLE-safe: texture()-based froxel sampling (no texelFetch), ASCII-only sources, single FragColor write.

//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/volumetric_segment.glsl"
//!include "common/ray_reconstruct.glsl"
//!include "common/volume_froxel_math.glsl"
//!include "common/volume_integrate_sample.glsl"

in vec2 vUv;
uniform sampler2DArray uFroxelVolume;
uniform sampler2DArray uPrevFroxelVolume;
uniform sampler2D uSceneDepth;
uniform sampler2D uPrevIntegrate;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
uniform vec3 uCameraPos;
uniform vec3 uPrevCameraPos;
uniform vec3 uCamRight;
uniform vec3 uCamUp;
uniform vec3 uCamForward;
uniform vec3 uPrevCamRight;
uniform vec3 uPrevCamUp;
uniform vec3 uPrevCamForward;
uniform vec3 uLightDir;
uniform vec3 uHalfExtent;
uniform vec3 uPrevHalfExtent;
uniform int uSliceCount;
uniform vec2 uFroxelTexelSize;
uniform float uStrength;
uniform float uJitter;
uniform float uTemporalWeight;
uniform float uFroxelTemporalWeight;
uniform float uDepthDistribution;
uniform float uScatterGain;
uniform float uExtinction;
uniform int uOutputClouds;
uniform int uHasPrevIntegrate;
uniform int uHasPrevFroxel;

out vec4 FragColor;

const int VM_STEPS = 36;
const float SKY_DEPTH_EPS = 0.9992;

vec3 softKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

vec2 viReprojectUv(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
{
    vec3 worldPos = grWorldPosFromUvDepth(uv, depth, invViewProj);
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
    if (uStrength <= 0.0)
    {
        discard;
    }

    vec3 rd = grWorldRayDir(vUv, uInvViewProj, uCameraPos);
    float receiverDepth = texture(uSceneDepth, vUv).r;

    if (uOutputClouds > 0 && (receiverDepth < SKY_DEPTH_EPS || rd.y < 0.04))
    {
        discard;
    }

    if (uOutputClouds == 0 && receiverDepth >= SKY_DEPTH_EPS)
    {
        discard;
    }

    vec3 sunToward = normalize(-uLightDir);
    float miePhase = atmosphereMiePhase(dot(rd, sunToward));

    float stepLen = uHalfExtent.z * 2.0 / float(VM_STEPS);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float jitter = uJitter * stepLen;
    float froxelTemporal = (uHasPrevFroxel > 0) ? uFroxelTemporalWeight : 0.0;

    for (int i = 0; i < VM_STEPS; ++i)
    {
        float t = jitter + (float(i) + 0.5) * stepLen;
        vec3 worldPos = uCameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, uCameraPos, uCamRight, uCamUp, uCamForward,
            uHalfExtent, uSliceCount, uDepthDistribution);
        float edgeW = vfFroxelEdgeWeight(froxelUv);
        if (edgeW <= 1e-5)
        {
            continue;
        }

        vec4 voxel = viSampleFroxel(uFroxelVolume, froxelUv, uSliceCount);
        if (froxelTemporal > 0.0)
        {
            vec3 prevUv = vfWorldToFroxelUv(worldPos, uPrevCameraPos, uPrevCamRight, uPrevCamUp, uPrevCamForward,
                uPrevHalfExtent, uSliceCount, uDepthDistribution);
            if (prevUv.x > 0.01 && prevUv.x < 0.99 && prevUv.y > 0.01 && prevUv.y < 0.99 && prevUv.z >= 0.0)
            {
                vec4 prevVoxel = viSampleFroxel(uPrevFroxelVolume, prevUv, uSliceCount);
                voxel = mix(voxel, prevVoxel, froxelTemporal);
            }
        }

        float density = voxel.r;
        if (density <= 1e-5)
        {
            continue;
        }

        vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.b * 0.92) * miePhase;
        float inscatterW = vmSegmentInscatterWeight(density, stepLen, uExtinction);
        accum += transmittance * sunScatter * inscatterW * uScatterGain * edgeW;
        transmittance *= mix(1.0, vmSegmentTransmittance(density, stepLen, uExtinction), edgeW);
        if (transmittance < 0.02)
        {
            break;
        }
    }

    vec3 vol = softKnee(accum * uStrength, 0.2);

    if (uOutputClouds == 0 && uHasPrevIntegrate > 0 && uTemporalWeight > 0.0)
    {
        vec2 prevUv = viReprojectUv(vUv, receiverDepth, uInvViewProj, uPrevViewProj);
        if (prevUv.x >= 0.0 && prevUv.x <= 1.0 && prevUv.y >= 0.0 && prevUv.y <= 1.0)
        {
            vec3 history = texture(uPrevIntegrate, prevUv).rgb;
            float histDepth = texture(uSceneDepth, prevUv).r;
            float depthValid = 1.0 - smoothstep(0.002, 0.02, abs(histDepth - receiverDepth));
            vol = mix(vol, history, uTemporalWeight * depthValid);
        }
    }

    float luma = max(max(vol.r, vol.g), vol.b);
    vec4 outColor;
    if (uOutputClouds > 0)
    {
        float alpha = saturate1(luma * 1.45);
        if (alpha <= 0.02)
        {
            discard;
        }

        outColor = vec4(linearToSrgb(vol), alpha);
    }
    else
    {
        if (luma <= 1e-6)
        {
            discard;
        }

        outColor = vec4(vol, 1.0);
    }

    FragColor = outColor;
}
