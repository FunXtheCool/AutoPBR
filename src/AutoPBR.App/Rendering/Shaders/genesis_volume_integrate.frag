#version 330 core
// Froxel integrate: Mie in-scatter along view rays (P3.2 / P3.3).

//!include "common/common.glsl"
//!include "common/tonemap.glsl"
//!include "common/godray_integration.glsl"
//!include "common/volume_froxel.glsl"

in vec2 vUv;
uniform sampler2DArray uFroxelVolume;
uniform sampler2D uSceneDepth;
uniform sampler2D uPrevIntegrate;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
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
uniform float uTemporalWeight;
uniform int uOutputClouds;
uniform int uHasPrevIntegrate;

out vec4 FragColor;

const int VM_STEPS = 36;
const float SKY_DEPTH_EPS = 0.9992;

vec3 softKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

vec2 viReprojectUv(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
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
    if (uStrength <= 0.0)
    {
        discard;
    }

    vec3 rd = grWorldRayDir(vUv, uInvViewProj, uCameraPos);
    float receiverDepth = texture(uSceneDepth, vUv).r;

    if (uOutputClouds > 0)
    {
        if (receiverDepth < SKY_DEPTH_EPS)
        {
            discard;
        }

        if (rd.y < 0.04)
        {
            discard;
        }
    }

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

    if (uOutputClouds > 0)
    {
        float alpha = saturate1(max(max(vol.r, vol.g), vol.b) * 1.45);
        if (alpha <= 0.02)
        {
            discard;
        }
        FragColor = vec4(linearToSrgb(vol), alpha);
        return;
    }

    if (max(max(vol.r, vol.g), vol.b) <= 1e-6)
    {
        discard;
    }

    FragColor = vec4(vol, 1.0);
}
