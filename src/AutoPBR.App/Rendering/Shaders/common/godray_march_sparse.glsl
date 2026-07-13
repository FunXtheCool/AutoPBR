// Sparse screen-space god-ray march helpers (fixed GR_SAMPLES loop bounds for GLES).

#ifndef GENESIS_GODRAY_MARCH_SPARSE_GLSL
#define GENESIS_GODRAY_MARCH_SPARSE_GLSL

float grSparseMarchT(int i, int sampleCount)
{
    int pairCount = max(sampleCount / 2, 1);
    int coarseIdx = i / 2;
    float coarseT = float(coarseIdx) / max(float(pairCount - 1), 1.0);
    if (mod(float(i), 2.0) < 0.5)
    {
        return coarseT;
    }

    float nextCoarseT = float(min(coarseIdx + 1, pairCount - 1)) / max(float(pairCount - 1), 1.0);
    return mix(coarseT, nextCoarseT, 0.5);
}

bool grSparseMarchSkipOddStepScreen(int i, float coarseBeamFalloff, bool coarseOccluded)
{
    if (mod(float(i), 2.0) < 0.5)
    {
        return false;
    }

    if (coarseBeamFalloff <= 0.01)
    {
        return true;
    }

    return coarseOccluded;
}

bool grSparseMarchSkipOddStepShadow(int i, float coarseBeamFalloff, bool coarseOccluded, bool coarseWasSky)
{
    if (mod(float(i), 2.0) < 0.5)
    {
        return false;
    }

    if (coarseWasSky)
    {
        return false;
    }

    if (coarseBeamFalloff <= 0.01)
    {
        return true;
    }

    return coarseOccluded;
}

#endif // GENESIS_GODRAY_MARCH_SPARSE_GLSL
