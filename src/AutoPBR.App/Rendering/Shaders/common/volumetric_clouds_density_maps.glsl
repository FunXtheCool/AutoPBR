// Cloud density model (not included on GLES froxel lite inject path).
// Remap chain: weather coverage -> Perlin-Worley base shape -> high-frequency edge erosion.

#ifndef GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL
#define GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL

//!include "volumetric_clouds_density.glsl"

float vcRemap01(float x, float a, float b)
{
    return saturate1((x - a) / max(b - a, 1e-5));
}

// High-altitude cirrus: isotropic FBM with domain warp so wisps break into natural patches
// instead of wind-aligned rows. Procedural and non-tiling.
float vcCirrusDensity(vec2 xz, vec2 windOffset, float volumeSize)
{
    vec2 p = (xz + windOffset) / max(volumeSize, 8.0);
    vec3 warp = vec3(
        vcFbm(vec3(p * 1.35, 2.17)),
        vcFbm(vec3(p.yx * 1.55 + 3.1, 4.83)),
        0.0);
    p += (warp.xy - 0.5) * 0.42;
    float n0 = vcFbm(vec3(p.x * 1.15, p.y * 1.08, 3.71));
    float n1 = vcFbm(vec3(p.x * 2.25 + 1.7, p.y * 1.95, 7.13));
    float n2 = 1.0 - vcFbm(vec3(p.x * 4.05, p.y * 3.55, 11.29));
    float d = n0 * 0.5 + n1 * 0.32 + n2 * 0.18;
    return vcRemap01(d, 0.48, 0.76);
}

// Weather sample: R = regional coverage, G = cloud type (0 low sheet .. 1 towering).
vec2 vcSampleWeather(sampler2D coverageMap, int hasCoverageMap, vec3 worldPos, float volumeSize, vec2 windOffset)
{
    if (hasCoverageMap < 1)
    {
        return vec2(0.55, 0.5);
    }

    float scale = max(volumeSize, 8.0);
    vec2 covUv = fract((worldPos.xz + windOffset) / (scale * 4.0) + 0.5);
    vec2 weather = texture(coverageMap, covUv).rg;
    return weather;
}

// Vertical density profile: soft bell peaks (not hard shelves) so the slab does not stack into
// visible horizontal rows when cloud type varies slowly across the weather map.
float vcHeightGradient(float h, float cloudType)
{
    float peak = mix(0.32, mix(0.52, 0.7, cloudType), saturate1(cloudType * 1.35));
    float spread = mix(0.42, 0.34, cloudType);
    float profile = exp(-pow((h - peak) / max(spread, 0.08), 2.0) * 2.2);
    float floorFade = smoothstep(0.0, 0.06, h) * (1.0 - smoothstep(0.94, 1.0, h));
    return profile * floorFade;
}

// Base shape without detail erosion. Cheap enough for the sun light march; the full
// density below erodes this with high-frequency detail for cauliflower silhouettes.
float vcCloudBaseDensity(vec3 worldPos, float layerBase, float layerTop, float coverageScale, float volumeSize,
    sampler3D cloudNoise, int hasCloudNoise, sampler2D coverageMap, int hasCoverageMap, vec3 windOffset)
{
    if (worldPos.y < layerBase || worldPos.y > layerTop)
    {
        return 0.0;
    }

    float layerH = max(layerTop - layerBase, 0.001);
    float h = (worldPos.y - layerBase) / layerH;

    vec2 weather = vcSampleWeather(coverageMap, hasCoverageMap, worldPos, volumeSize, windOffset.xz);
    float coverage = saturate1(weather.x * coverageScale);
    if (coverage <= 1e-3)
    {
        return 0.0;
    }

    float heightGrad = vcHeightGradient(h, weather.y);
    if (heightGrad <= 1e-3)
    {
        return 0.0;
    }

    float sizeScale = max(volumeSize, 8.0) * 2.0;
    vec3 warpPos = (worldPos + windOffset) / sizeScale;
    vec3 warp = vec3(
        vcFbm(warpPos * 2.0 + vec3(17.0, 3.0, 29.0)),
        vcFbm(warpPos * 2.0 + vec3(41.0, 11.0, 7.0)),
        vcFbm(warpPos * 2.0 + vec3(5.0, 37.0, 19.0)));
    vec3 shapeUvw = fract(warpPos + (warp - 0.5) * 0.16);
    float base;
    if (hasCloudNoise > 0)
    {
        vec4 n = texture(cloudNoise, shapeUvw);
        float shapeFbm = n.g * 0.625 + n.b * 0.25 + n.a * 0.125;
        // Carve the Perlin-Worley base with the Worley FBM (remap toward its envelope).
        base = vcRemap01(n.r, shapeFbm - 1.0, 1.0);
    }
    else
    {
        base = vcFbm((worldPos + windOffset) / max(volumeSize, 8.0) * 2.0);
    }

    base *= heightGrad;
    // Coverage erosion: low coverage eats the shape from the outside in, then scales it,
    // so sparse skies hold a few full clouds instead of many faint ones.
    float baseShaped = vcRemap01(base, 1.0 - coverage, 1.0) * coverage;
    return baseShaped;
}


// Full density from a precomputed base shape (avoids duplicate base-density work in the march loop).
float vcCloudDensityFromBase(float base, vec3 worldPos, float layerBase, float layerTop, float densityMul,
    float volumeSize, sampler3D detailNoise, int hasDetailNoise, vec3 windOffset)
{
    if (base <= 1e-4)
    {
        return 0.0;
    }

    float erode = 0.0;
    if (hasDetailNoise > 0)
    {
        float layerH = max(layerTop - layerBase, 0.001);
        float h = (worldPos.y - layerBase) / layerH;
        float detailScale = max(volumeSize, 8.0) * 0.5;
        vec3 detailUvw = fract((worldPos + windOffset * 0.5) / detailScale);
        vec3 dn = texture(detailNoise, detailUvw).rgb;
        float detailFbm = dn.r * 0.625 + dn.g * 0.25 + dn.b * 0.125;
        detailFbm = mix(detailFbm, 1.0 - detailFbm, saturate1(h * 5.0));
        erode = detailFbm * 0.35;
    }

    float density = vcRemap01(base, erode, 1.0);
    return density * densityMul;
}

// Full density: base shape eroded by high-frequency detail at cloud edges.
float vcCloudDensityEx(vec3 worldPos, float layerBase, float layerTop, float densityMul, float coverageScale,
    float volumeSize, sampler3D cloudNoise, int hasCloudNoise, sampler3D detailNoise, int hasDetailNoise,
    sampler2D coverageMap, int hasCoverageMap, vec3 windOffset)
{
    float base = vcCloudBaseDensity(worldPos, layerBase, layerTop, coverageScale, volumeSize,
        cloudNoise, hasCloudNoise, coverageMap, hasCoverageMap, windOffset);
    return vcCloudDensityFromBase(base, worldPos, layerBase, layerTop, densityMul, volumeSize,
        detailNoise, hasDetailNoise, windOffset);
}

#endif // GENESIS_VOLUMETRIC_CLOUDS_DENSITY_MAPS_GLSL
