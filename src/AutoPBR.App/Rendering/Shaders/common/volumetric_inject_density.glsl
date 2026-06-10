// GLES-safe inject density: height fog + analytic slab cloud (no FBM loops).

#ifndef GENESIS_VOLUMETRIC_INJECT_DENSITY_GLSL
#define GENESIS_VOLUMETRIC_INJECT_DENSITY_GLSL

float viHeightFogDensity(vec3 worldPos, float groundWorldY, float fogSlabTopY, float strength)
{
    if (strength <= 0.0)
    {
        return 0.0;
    }

    float heightAboveGround = worldPos.y - groundWorldY;
    if (heightAboveGround < 0.0 || heightAboveGround > fogSlabTopY)
    {
        return 0.0;
    }

    float heightFactor = exp(-heightAboveGround * 0.38);
    return heightFactor * strength * 0.3;
}

float viSlabCloudDensity(vec3 worldPos, float layerBase, float layerTop, float densityMul)
{
    if (worldPos.y < layerBase || worldPos.y > layerTop)
    {
        return 0.0;
    }

    float layerH = max(layerTop - layerBase, 0.001);
    float h = (worldPos.y - layerBase) / layerH;
    float heightFade = smoothstep(0.0, 0.12, h) * smoothstep(1.0, 0.58, h);
    return heightFade * densityMul * 0.35;
}

float viInjectMediumDensity(vec3 worldPos, float groundWorldY, float fogSlabTopY, float layerBase, float layerTop,
    float cloudDensityMul, float heightFogStrength)
{
    float fog = viHeightFogDensity(worldPos, groundWorldY, fogSlabTopY, heightFogStrength);
    float cloud = viSlabCloudDensity(worldPos, layerBase, layerTop, cloudDensityMul);
    return fog + cloud;
}

#endif // GENESIS_VOLUMETRIC_INJECT_DENSITY_GLSL
