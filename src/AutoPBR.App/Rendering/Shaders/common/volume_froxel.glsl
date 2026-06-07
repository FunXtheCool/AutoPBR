// Camera-aligned froxel volume helpers (P3).

#ifndef GENESIS_VOLUME_FROXEL_GLSL
#define GENESIS_VOLUME_FROXEL_GLSL

//!include "atmosphere.glsl"

const int VF_MAX_SLICES = 32;

vec3 vfFroxelWorldPos(vec2 uv01, int sliceIndex, int sliceCount, vec3 cameraPos,
    vec3 camRight, vec3 camUp, vec3 camForward, vec3 halfExtent)
{
    float z = (float(sliceIndex) + 0.5) / float(sliceCount);
    vec3 local = vec3(
        (uv01.x * 2.0 - 1.0) * halfExtent.x,
        (uv01.y * 2.0 - 1.0) * halfExtent.y,
        z * halfExtent.z * 2.0);
    return cameraPos + camRight * local.x + camUp * local.y + camForward * local.z;
}

vec3 vfWorldToFroxelUv(vec3 worldPos, vec3 cameraPos, vec3 camRight, vec3 camUp, vec3 camForward,
    vec3 halfExtent, int sliceCount)
{
    vec3 local = worldPos - cameraPos;
    vec3 uv;
    uv.x = dot(local, camRight) / max(halfExtent.x, 1e-3) * 0.5 + 0.5;
    uv.y = dot(local, camUp) / max(halfExtent.y, 1e-3) * 0.5 + 0.5;
    uv.z = dot(local, camForward) / max(halfExtent.z * 2.0, 1e-3);
    uv.z = clamp(uv.z, 0.0, 1.0) * float(sliceCount);
    return uv;
}

vec4 vfSampleFroxel(sampler2DArray froxelTex, vec3 froxelUv, int sliceCount, vec2 texelSize)
{
    float z = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
    float z0 = floor(z);
    float z1 = min(z0 + 1.0, float(sliceCount) - 1.0);
    float fz = z - z0;

    vec2 uv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
    vec4 s0 = texture(froxelTex, vec3(uv, z0));
    vec4 s1 = texture(froxelTex, vec3(uv, z1));
    return mix(s0, s1, fz);
}

#endif // GENESIS_VOLUME_FROXEL_GLSL
