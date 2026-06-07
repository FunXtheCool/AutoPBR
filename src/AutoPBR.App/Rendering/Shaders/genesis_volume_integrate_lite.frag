#version 330 core
// Lite froxel integrate for GLES fallback: god rays only, no temporal or cloud output.

//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/volume_froxel.glsl"

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

out vec4 FragColor;

const int VM_STEPS = 28;

vec3 worldRayDir(vec2 uv, mat4 invViewProj, vec3 cameraPos)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    vec4 worldH = invViewProj * vec4(ndc, 1.0, 1.0);
    vec3 farPt = worldH.xyz / max(worldH.w, 1e-6);
    vec3 rd = farPt - cameraPos;
    float len2 = dot(rd, rd);
    if (len2 < 1e-12)
    {
        return vec3(0.0, 1.0, 0.0);
    }
    return rd * inversesqrt(len2);
}

void main()
{
    if (uStrength <= 0.0)
    {
        discard;
    }

    vec3 rd = worldRayDir(vUv, uInvViewProj, uCameraPos);
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
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, uCameraPos, uCamRight, uCamUp, uCamForward, uHalfExtent, uSliceCount);
        if (froxelUv.x < 0.01 || froxelUv.x > 0.99 || froxelUv.y < 0.01 || froxelUv.y > 0.99 || froxelUv.z < 0.0)
        {
            continue;
        }

        vec4 voxel = vfSampleFroxel(uFroxelVolume, froxelUv, uSliceCount, uFroxelTexelSize);
        float density = voxel.r;
        if (density <= 1e-5)
        {
            continue;
        }

        vec3 sunScatter = voxel.gba * miePhase;
        accum += transmittance * sunScatter * stepLen * 3.8;
        transmittance *= exp(-density * stepLen * 1.15);
        if (transmittance < 0.02)
        {
            break;
        }
    }

    vec3 vol = (accum * uStrength) / (accum * uStrength + vec3(0.2));
    if (max(max(vol.r, vol.g), vol.b) <= 1e-6)
    {
        discard;
    }

    FragColor = vec4(vol, 1.0);
}
