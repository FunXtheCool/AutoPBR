// Shared cloud lighting: dual-lobe Henyey-Greenstein, multi-scatter octaves, sun light march.

#ifndef GENESIS_VOLUMETRIC_CLOUDS_GLSL
#define GENESIS_VOLUMETRIC_CLOUDS_GLSL

//!include "atmosphere.glsl"
//!include "volumetric_clouds_density_maps.glsl"

float vcHGPhase(float cosTheta, float g)
{
    float gg = g * g;
    float denom = max(pow(1.0 + gg - 2.0 * g * cosTheta, 1.5), 1e-4);
    return (1.0 - gg) / (4.0 * 3.14159265 * denom);
}

// Forward lobe plus a soft back lobe so clouds opposite the sun keep silver rims.
float vcDualLobePhase(float cosTheta, float g)
{
    return mix(vcHGPhase(cosTheta, -0.35 * g), vcHGPhase(cosTheta, g), 0.7);
}

// Multi-scattering approximation: each octave re-runs the single-scatter estimate with
// extinction, phase eccentricity, and intensity scaled down, so optically thick cores
// glow instead of clamping to black under pure Beer-Lambert.
vec3 vcSunScatter(vec3 sunColor, float cosTheta, float lightOd)
{
    vec3 sum = vec3(0.0);
    float extScale = 1.0;
    float phaseG = 0.72;
    float intensity = 1.0;
    for (int o = 0; o < 3; ++o)
    {
        float phase = vcDualLobePhase(cosTheta, phaseG);
        sum += sunColor * (intensity * phase * exp(-lightOd * extScale));
        extScale *= 0.5;
        phaseG *= 0.5;
        intensity *= 0.55;
    }

    // Gentle powder shaping: thin sun-facing edges dim slightly instead of going black.
    float powder = 1.0 - exp(-lightOd * 2.0);
    return sum * mix(0.6, 1.0, powder);
}

// Sun radiance at cloud altitude: warms and extinguishes as the sun drops to the horizon,
// matching the warm band the sky dome renders at sunrise/sunset.
vec3 vcCloudSunColor(vec3 sunToward, float sunIntensity)
{
    float sunElev = clamp(sunToward.y, -1.0, 1.0);
    float lowSun = 1.0 - smoothstep(0.04, 0.42, max(sunElev, 0.0));
    vec3 col = mix(vec3(1.0, 0.97, 0.92), vec3(1.0, 0.52, 0.24), lowSun);
    float horizonExtinction = smoothstep(-0.08, 0.12, sunElev);
    return col * horizonExtinction * clamp(sunIntensity * 0.12, 0.0, 2.0);
}

// Light march toward the sun for self-shadowing. Exponential step distribution keeps
// resolution near the sample point, and one distant coarse tap catches far occluders.
// Samples the base shape (no detail erosion) so shadows track the rendered clouds cheaply.
float vcLightOpticalDepth(vec3 worldPos, vec3 sunToward, float layerBase, float layerTop,
    float densityMul, float coverageScale, float volumeSize, int lightSteps,
    sampler3D cloudNoise, int hasCloudNoise, sampler2D coverageMap, int hasCoverageMap, vec3 windOffset)
{
    const float range = 22.0;
    float od = 0.0;
    float tPrev = 0.0;
    for (int i = 0; i < 6; ++i)
    {
        if (i >= lightSteps)
        {
            break;
        }

        float frac = (float(i) + 1.0) / float(max(lightSteps, 1));
        float t = frac * frac * range;
        float dt = t - tPrev;
        vec3 samplePos = worldPos + sunToward * (tPrev + dt * 0.5);
        tPrev = t;
        if (samplePos.y < layerBase || samplePos.y > layerTop)
        {
            break;
        }

        od += vcCloudBaseDensity(samplePos, layerBase, layerTop, coverageScale, volumeSize,
            cloudNoise, hasCloudNoise, coverageMap, hasCoverageMap, windOffset) * densityMul * dt * 0.18;
    }

    vec3 farPos = worldPos + sunToward * (range * 2.2);
    if (farPos.y >= layerBase && farPos.y <= layerTop)
    {
        od += vcCloudBaseDensity(farPos, layerBase, layerTop, coverageScale, volumeSize,
            cloudNoise, hasCloudNoise, coverageMap, hasCoverageMap, windOffset) * densityMul * range * 0.5 * 0.18;
    }

    return od;
}

#endif // GENESIS_VOLUMETRIC_CLOUDS_GLSL
