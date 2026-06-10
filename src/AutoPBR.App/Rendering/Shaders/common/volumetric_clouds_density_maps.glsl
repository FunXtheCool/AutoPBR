// Cloud density with optional 3D noise + coverage maps (not included on GLES froxel lite inject path).

#ifndef GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL
#define GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL

//!include "volumetric_clouds_density.glsl"

float vcSampleNoiseBase(sampler3D cloudNoise, int hasCloudNoise, vec3 samplePos)
{
    if (hasCloudNoise > 0)
    {
        vec3 n = texture(cloudNoise, fract(samplePos * 0.08 + 0.5)).rgb;
        return n.r * 0.55 + n.g * 0.35 + n.b * 0.10;
    }

    return vcFbm(samplePos * 0.52);
}

float vcSampleCoverageMap(sampler2D coverageMap, int hasCoverageMap, vec3 worldPos, float volumeSize)
{
    if (hasCoverageMap < 1)
    {
        return 1.0;
    }

    float scale = max(volumeSize, 8.0);
    vec2 covUv = fract(worldPos.xz / (scale * 4.0) + 0.5);
    return texture(coverageMap, covUv).r;
}

float vcCloudDensityEx(vec3 worldPos, float layerBase, float layerTop, float densityMul, float volumeSize,
    sampler3D cloudNoise, int hasCloudNoise, sampler2D coverageMap, int hasCoverageMap)
{
    if (worldPos.y < layerBase || worldPos.y > layerTop)
    {
        return 0.0;
    }

    float layerH = max(layerTop - layerBase, 0.001);
    float h = (worldPos.y - layerBase) / layerH;
    float heightFade = smoothstep(0.0, 0.12, h) * smoothstep(1.0, 0.58, h);

    float sizeScale = max(volumeSize, 8.0);
    vec3 samplePos = worldPos / sizeScale;
    float base = vcSampleNoiseBase(cloudNoise, hasCloudNoise, samplePos + vec3(0.0, h * 1.8, 0.0));
    float detail = 1.0 - vcFbm(samplePos * 1.75 + vec3(41.0, 17.0, 9.0));
    float regional = vcSampleCoverageMap(coverageMap, hasCoverageMap, worldPos, volumeSize);
    float coverage = saturate1((base * 0.62 + detail * 0.38 - 0.15) * 3.8 * densityMul * regional);
    return coverage * heightFade;
}

#endif // GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL
