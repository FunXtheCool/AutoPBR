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
    float denom = pow(max(1.0 + gg - 2.0 * g * cosTheta, 1e-3), 1.5);
    return (1.0 - gg) / (4.0 * ATM_PI * denom);
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

#endif // GENESIS_ATMOSPHERE_GLSL
