// Camera-aligned froxel volume helpers (integrate path - temporal only; fetch is macro-inlined).

#ifndef GENESIS_VOLUME_FROXEL_GLSL
#define GENESIS_VOLUME_FROXEL_GLSL

//!include "atmosphere.glsl"
//!include "volume_froxel_math.glsl"

vec4 vfTemporalBlendFroxel(vec4 current, vec4 history, float weight, float valid)
{
    if (weight <= 0.0 || valid <= 0.0)
    {
        return current;
    }

    float w = weight * valid;
    vec4 blended;
    blended.r = mix(current.r, history.r, w);
    blended.g = mix(current.g, history.g, w);
    blended.b = mix(current.b, history.b, w);
    blended.a = max(current.a, history.a);
    return blended;
}

#endif // GENESIS_VOLUME_FROXEL_GLSL
