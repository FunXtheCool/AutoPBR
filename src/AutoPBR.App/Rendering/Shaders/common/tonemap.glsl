// Genesis preview shader - tonemapping.
// ACES Filmic (Narkowicz fit). Cheap and widely accepted as the preview standard.
// Reference: https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/

#ifndef GENESIS_TONEMAP_GLSL
#define GENESIS_TONEMAP_GLSL

//!include "common.glsl"

vec3 tonemapAcesNarkowicz(vec3 x)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return saturate3((x * (a * x + b)) / (x * (c * x + d) + e));
}

#endif // GENESIS_TONEMAP_GLSL
