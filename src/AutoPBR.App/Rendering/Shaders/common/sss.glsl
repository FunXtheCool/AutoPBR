// Genesis preview shader - single-pass subsurface-scattering approximation.
// Real Iris SSS integrates shadow-map depth and is not available here. We approximate with:
//  * Wrapped Lambert (Burley) for soft front-lighting,
//  * Translucency lobe driven by view-light alignment for back/edge-lit transmission,
//  * A cosmetic porosity darkening cue when LabPBR _s.b <= 64.

#ifndef GENESIS_SSS_GLSL
#define GENESIS_SSS_GLSL

//!include "common.glsl"

float wrappedNdotL(vec3 N, vec3 L, float w)
{
    float ndl = dot(N, L);
    float wrap = (ndl + w) / pow(1.0 + w, 2.0);
    return clamp(wrap, 0.0, 1.0);
}

vec3 sssWrappedDiffuse(vec3 N, vec3 L, vec3 albedo, float sssAmount, vec3 lightColor)
{
    float w = mix(0.0, 0.5, sssAmount);
    float wn = wrappedNdotL(N, L, w);
    return albedo * lightColor * wn * sssAmount;
}

// Cheap translucency lobe. Light passes through the surface and exits roughly along -L, so the
// view sees a glow when looking against the light through a thin object.
vec3 sssTransmission(vec3 V, vec3 L, vec3 albedo, float sssAmount, vec3 lightColor)
{
    float t = clamp(dot(-L, V), 0.0, 1.0);
    float lobe = pow(t, 4.0);
    return albedo * lightColor * lobe * sssAmount;
}

// Cosmetic porosity cue: pack porosity (b <= 64) hints "rain darkens this surface" in real Iris.
// We do not simulate wetness here, but we slightly darken albedo so the channel is visually
// distinguishable. Returns a multiplier suitable for albedo *= (...).
float porosityAlbedoMultiplier(float porosity)
{
    return mix(1.0, 0.92, porosity);
}

#endif // GENESIS_SSS_GLSL
