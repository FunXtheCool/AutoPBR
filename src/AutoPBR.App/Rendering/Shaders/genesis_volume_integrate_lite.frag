#version 330 core
// GENESIS_GLES_PACK rev29
// Lite froxel integrate: view-ray Mie in-scatter march -> god-ray RGBA. No temporal / cloud path.
// ANGLE-safe: texture()-based froxel sampling (no texelFetch), ASCII-only sources.

//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/volumetric_segment.glsl"
//!include "common/ray_reconstruct.glsl"
//!include "common/volume_froxel_math.glsl"
//!include "common/volume_integrate_sample.glsl"

in vec2 vUv;
uniform sampler2DArray uFroxelVolume;
uniform sampler2D uSceneDepth;
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uCamRight;
uniform vec3 uCamUp;
uniform vec3 uCamForward;
uniform vec3 uLightDir;
uniform vec3 uHalfExtent;
uniform int uSliceCount;
uniform vec2 uFroxelTexelSize;
uniform float uStrength;
uniform float uJitter;
uniform float uDepthDistribution;
uniform float uScatterGain;
uniform float uExtinction;

out vec4 FragColor;

const int VM_STEPS = 24;
const float SKY_DEPTH_EPS = 0.9992;

vec3 softKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

void main()
{
    if (uStrength <= 0.0)
    {
        discard;
    }

    if (texture(uSceneDepth, vUv).r >= SKY_DEPTH_EPS)
    {
        discard;
    }

    vec3 rd = grWorldRayDir(vUv, uInvViewProj, uCameraPos);
    vec3 sunToward = normalize(-uLightDir);
    float miePhase = atmosphereMiePhase(dot(rd, sunToward));

    float stepLen = uHalfExtent.z * 2.0 / float(VM_STEPS);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float jitter = uJitter * stepLen;

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
    if (max(max(vol.r, vol.g), vol.b) <= 1e-6)
    {
        discard;
    }

    FragColor = vec4(vol, 1.0);
}
