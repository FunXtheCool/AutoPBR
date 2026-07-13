#ifndef GENESIS_ATMOSPHERE_GLSL
#define GENESIS_ATMOSPHERE_GLSL

const float ATM_PI = 3.14159265358979323846;

vec3 atmosphereBetaRayleigh()
{
    // Approximate sea-level Rayleigh coefficients for RGB wavelengths.
    return vec3(5.5e-6, 13.0e-6, 22.4e-6);
}

float atmosphereMiePhase(float cosTheta)
{
    // Henyey-Greenstein phase with moderate forward scattering.
    const float g = 0.76;
    float gg = g * g;
    float base = max(1.0 + gg - 2.0 * g * cosTheta, 1e-3);
    return (1.0 - gg) / (4.0 * ATM_PI * base * sqrt(base));
}

float atmosphereRayleighPhase(float cosTheta)
{
    return (3.0 / (16.0 * ATM_PI)) * (1.0 + cosTheta * cosTheta);
}

float atmosphereAirDensity(float h)
{
    return exp(-max(h, 0.0) / 8.0);
}

float atmosphereAerosolDensity(float h, float turbidity)
{
    float scaleKm = mix(1.8, 0.6, clamp((turbidity - 1.0) / 9.0, 0.0, 1.0));
    return exp(-max(h, 0.0) / scaleKm);
}

vec3 atmosphereSunColor(float sunIntensity)
{
    return vec3(1.0, 0.97, 0.92) * sunIntensity;
}

// Warm sun disk/sky tint: orange-red at horizon, warm white near zenith.
vec3 atmosphereSunWarmColor(float sunIntensity, float sunElevation)
{
    vec3 horizonWarm = vec3(1.0, 0.62, 0.28);
    vec3 zenithWarm = vec3(1.0, 0.96, 0.86);
    return mix(horizonWarm, zenithWarm, smoothstep(0.0, 0.42, max(sunElevation, 0.0))) * sunIntensity;
}

#endif // GENESIS_ATMOSPHERE_GLSL
