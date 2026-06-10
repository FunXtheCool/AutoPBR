// Beer-Lambert segment helpers (integrate path - no cloud density includes).

#ifndef GENESIS_VOLUMETRIC_SEGMENT_GLSL
#define GENESIS_VOLUMETRIC_SEGMENT_GLSL

float vmSegmentTransmittance(float density, float stepLen, float extinction)
{
    return exp(-density * stepLen * extinction);
}

float vmSegmentInscatterWeight(float density, float stepLen, float extinction)
{
    float od = density * stepLen * extinction;
    return 1.0 - exp(-od);
}

#endif // GENESIS_VOLUMETRIC_SEGMENT_GLSL
