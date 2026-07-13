// Sparse / hierarchical froxel march helpers (fixed VM_STEPS loop bounds for GLES).

#ifndef GENESIS_VOLUME_INTEGRATE_SPARSE_GLSL
#define GENESIS_VOLUME_INTEGRATE_SPARSE_GLSL

float viSampleFroxelOccupancy(sampler2DArray occupancyTex, vec3 froxelUv, int sliceCount)
{
    if (froxelUv.x < 0.01 || froxelUv.x > 0.99 || froxelUv.y < 0.01 || froxelUv.y > 0.99 || froxelUv.z < 0.0)
    {
        return 0.0;
    }

    vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
    float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
    return texture(occupancyTex, vec3(vfUv, sliceCoord)).r;
}

float viSparseMarchT(int i, float jitter, float stepLenCoarse, float stepLenFine)
{
    int coarseIdx = i / 2;
    if (mod(float(i), 2.0) < 0.5)
    {
        return jitter + (float(coarseIdx) + 0.5) * stepLenCoarse;
    }

    float coarseT = jitter + (float(coarseIdx) + 0.5) * stepLenCoarse;
    return coarseT + stepLenFine * 0.5;
}

bool viSparseMarchSkipOddStep(
    int i,
    float jitter,
    float stepLenCoarse,
    vec3 rd,
    vec3 cameraPos,
    vec3 camRight,
    vec3 camUp,
    vec3 camForward,
    vec3 halfExtent,
    int sliceCount,
    float depthDistribution,
    sampler2DArray occupancyTex)
{
    if (mod(float(i), 2.0) < 0.5)
    {
        return false;
    }

    int coarseIdx = i / 2;
    float coarseT = jitter + (float(coarseIdx) + 0.5) * stepLenCoarse;
    vec3 coarseWorld = cameraPos + rd * coarseT;
    vec3 coarseFroxel = vfWorldToFroxelUv(coarseWorld, cameraPos, camRight, camUp, camForward,
        halfExtent, sliceCount, depthDistribution);
    return viSampleFroxelOccupancy(occupancyTex, coarseFroxel, sliceCount) <= 1e-5;
}

#endif // GENESIS_VOLUME_INTEGRATE_SPARSE_GLSL
