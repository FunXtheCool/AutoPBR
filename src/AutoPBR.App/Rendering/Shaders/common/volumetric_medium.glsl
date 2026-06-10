// Unified participating-medium density (P3 foundation).
// Cloud slab + world-anchored ground fog; shared by froxel inject and screen-space gates.

#ifndef GENESIS_VOLUMETRIC_MEDIUM_GLSL
#define GENESIS_VOLUMETRIC_MEDIUM_GLSL

//!include "volumetric_clouds_density.glsl"
//!include "volumetric_segment.glsl"

// Ground-hugging mist slab in world Y (not camera-relative - avoids orbit grey dome).
float vmHeightFogDensity(vec3 worldPos, float groundWorldY, float fogSlabTopY, float strength)
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

float vmMediumDensity(vec3 worldPos, float groundWorldY, float fogSlabTopY, float layerBase, float layerTop,
    float cloudDensityMul, float volumeSize, float heightFogStrength)
{
    float cloud = vcCloudDensityRaw(worldPos, layerBase, layerTop, cloudDensityMul, volumeSize);
    float fog = vmHeightFogDensity(worldPos, groundWorldY, fogSlabTopY, heightFogStrength);
    return cloud + fog;
}

float vmMediumTransmittance(float density, float scale)
{
    return exp(-density * scale);
}

#endif // GENESIS_VOLUMETRIC_MEDIUM_GLSL
