#version 330 core
// Froxel inject: density + sun-lit scatter into a 2D array (P3.2).

//!include "common/common.glsl"
//!include "common/godray_integration.glsl"
//!include "common/volume_froxel.glsl"

in vec2 vUv;
uniform sampler2DShadow uShadowMap;
uniform sampler2DShadow uShadowMapNear;
uniform mat4 uLightViewProj;
uniform mat4 uLightViewProjNear;
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
uniform vec2 uShadowTexelSize;
uniform float uShadowMinBias;
uniform int uEnableShadowMap;
uniform int uEnableShadowCascades;
uniform float uCascadeSplitDistance;

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

    float shadowT = grShadowGateCascaded(worldPos, uCameraPos, uLightViewProjNear, uLightViewProj,
        uShadowMapNear, uShadowMap, uShadowTexelSize, uShadowMinBias, uEnableShadowMap,
        uEnableShadowCascades, uCascadeSplitDistance);
    vec3 sunLit = uLightColor * density * shadowT * 0.85;
    vec4 packed;
    packed.r = density;
    packed.gba = sunLit;
    FragColor = packed;
}
