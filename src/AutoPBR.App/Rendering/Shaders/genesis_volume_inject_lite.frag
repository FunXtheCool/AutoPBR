#version 330 core
// Lite froxel inject for GLES fallback: no shadow maps.

//!include "common/common.glsl"
//!include "common/volumetric_medium.glsl"
//!include "common/volume_froxel.glsl"

in vec2 vUv;
uniform vec3 uCameraPos;
uniform vec3 uCamRight;
uniform vec3 uCamUp;
uniform vec3 uCamForward;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uHalfExtent;
uniform int uSliceIndex;
uniform int uSliceCount;
uniform float uLayerHeight;
uniform float uVolumeHeight;
uniform float uCloudDensity;
uniform float uVolumeSize;
uniform float uGroundWorldY;
uniform float uFogSlabHeight;
uniform float uHeightFogStrength;

out vec4 FragColor;

void main()
{
    vec3 worldPos = vfFroxelWorldPos(vUv, uSliceIndex, uSliceCount, uCameraPos, uCamRight, uCamUp, uCamForward, uHalfExtent);
    float layerBase = uLayerHeight;
    float layerTop = layerBase + uVolumeHeight;
    float density = vmMediumDensity(worldPos, uGroundWorldY, uFogSlabHeight, layerBase, layerTop,
        uCloudDensity, uVolumeSize, uHeightFogStrength);
    if (density <= 1e-5)
    {
        FragColor = vec4(0.0, 0.0, 0.0, 0.0);
        return;
    }

    vec3 sunLit = uLightColor * density * 0.85;
    vec4 packed;
    packed.r = density;
    packed.gba = sunLit;
    FragColor = packed;
}
