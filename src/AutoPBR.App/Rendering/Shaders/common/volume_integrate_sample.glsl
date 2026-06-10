// ANGLE-safe froxel sampling: texture() (not texelFetch) with manual two-slice blend.
// texture(sampler2DArray) filters within a layer but snaps the layer index, so we sample
// the two bracketing slices and lerp. Sampler is passed as a parameter (valid in GLSL ES 3.0)
// so this works for both the live and history froxel volumes.

#ifndef GENESIS_VOLUME_INTEGRATE_SAMPLE_GLSL
#define GENESIS_VOLUME_INTEGRATE_SAMPLE_GLSL

vec4 viSampleFroxel(sampler2DArray vol, vec3 froxelUv, int sliceCount)
{
    vec2 uv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
    // Slice i is stored at coordinate (i + 0.5); shift by 0.5 to find the bracketing layers.
    float zc = clamp(froxelUv.z - 0.5, 0.0, float(sliceCount) - 1.0);
    float z0 = floor(zc);
    float z1 = min(z0 + 1.0, float(sliceCount) - 1.0);
    float fz = zc - z0;
    vec4 s0 = texture(vol, vec3(uv, z0));
    vec4 s1 = texture(vol, vec3(uv, z1));
    return mix(s0, s1, fz);
}

#endif // GENESIS_VOLUME_INTEGRATE_SAMPLE_GLSL
