// Shared cloud lighting: Henyey-Greenstein, Beer-Powder, 3D noise sampling.

#ifndef GENESIS_VOLUMETRIC_CLOUDS_GLSL
#define GENESIS_VOLUMETRIC_CLOUDS_GLSL

//!include "atmosphere.glsl"

float vcHGPhase(float cosTheta, float g)
{
    float gg = g * g;
    float denom = max(pow(1.0 + gg - 2.0 * g * cosTheta, 1.5), 1e-4);
    return (1.0 - gg) / (4.0 * 3.14159265 * denom);
}

float vcBeerPowder(float opticalDepth)
{
    float beer = exp(-opticalDepth);
    float powder = 1.0 - exp(-opticalDepth * 2.0);
    return beer * powder;
}

// Light march toward the sun for self-shadowing (few steps, cone widening).
float vcLightOpticalDepth(vec3 worldPos, vec3 sunToward, float layerBase, float layerTop,
    float densityMul, float volumeSize, int lightSteps)
{
    float od = 0.0;
    float stepLen = 18.0 / float(max(lightSteps, 1));
    for (int i = 0; i < 6; ++i)
    {
        if (i >= lightSteps)
        {
            break;
        }

        float t = (float(i) + 0.5) * stepLen;
        vec3 samplePos = worldPos + sunToward * t;
        if (samplePos.y < layerBase || samplePos.y > layerTop)
        {
            break;
        }

        od += vcCloudDensityRaw(samplePos, layerBase, layerTop, densityMul, volumeSize) * stepLen * 0.14;
    }

    return od;
}

vec3 vcCloudScatterColor(vec3 sunColor, float miePhase, float hgPhase, float beerPowder, float density)
{
    return sunColor * (miePhase * 0.35 + hgPhase * 0.65) * beerPowder * density;
}

#endif // GENESIS_VOLUMETRIC_CLOUDS_GLSL
