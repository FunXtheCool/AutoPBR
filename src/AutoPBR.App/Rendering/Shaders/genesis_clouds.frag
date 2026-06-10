#version 330 core
// Screen-space volumetric cloud layer (sky pixels only) with Beer-Powder lighting.

//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/volumetric_clouds.glsl"
//!include "common/volumetric_medium.glsl"
//!include "common/volumetric_clouds_density_maps.glsl"

in vec2 vUv;
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uSunDir;
uniform vec3 uSunColor;
uniform sampler2D uSkyViewLut;
uniform sampler3D uCloudNoise;
uniform sampler2D uCoverageMap;
uniform sampler2D uSceneDepth;
uniform sampler2D uPrevClouds;
uniform mat4 uPrevViewProj;
uniform float uLayerHeight;
uniform float uVolumeHeight;
uniform float uDensity;
uniform float uVolumeSize;
uniform int uQuality;
uniform int uGateSkyDepth;
uniform int uHasCloudNoise;
uniform int uHasCoverageMap;
uniform int uHasSkyLut;
uniform int uHasPrevClouds;
uniform float uTemporalWeight;
uniform float uFramePhase;

out vec4 FragColor;

const int CLOUD_STEPS = 24;
const float SKY_DEPTH_EPS = 0.9992;

vec3 worldRayDir(vec2 uv, mat4 invViewProj, vec3 cameraPos)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    vec4 worldH = invViewProj * vec4(ndc, 1.0, 1.0);
    vec3 farPt = worldH.xyz / max(worldH.w, 1e-6);
    vec3 rd = farPt - cameraPos;
    float len2 = dot(rd, rd);
    if (len2 < 1e-12)
    {
        return vec3(0.0, 1.0, 0.0);
    }
    return rd * inversesqrt(len2);
}

vec3 sampleSkyAmbient(vec3 rd, sampler2D skyLut, int hasSkyLut)
{
    if (hasSkyLut < 1)
    {
        return vec3(0.55, 0.62, 0.72);
    }

    float u = atan(rd.z, rd.x) / (2.0 * 3.14159265) + 0.5;
    float v = 0.5 - asin(clamp(rd.y, -1.0, 1.0)) / 3.14159265;
    return texture(skyLut, vec2(u, v)).rgb;
}

void main()
{
    if (uGateSkyDepth > 0)
    {
        float sceneDepth = texture(uSceneDepth, vUv).r;
        if (sceneDepth < SKY_DEPTH_EPS)
        {
            discard;
        }
    }

    vec3 rd = worldRayDir(vUv, uInvViewProj, uCameraPos);
    if (rd.y < 0.02)
    {
        discard;
    }

    float layerTop = uLayerHeight + uVolumeHeight;
    float tEnter = (uLayerHeight - uCameraPos.y) / max(rd.y, 1e-4);
    float tExit = (layerTop - uCameraPos.y) / max(rd.y, 1e-4);
    if (tEnter > tExit)
    {
        float tmp = tEnter;
        tEnter = tExit;
        tExit = tmp;
    }

    tEnter = max(tEnter, 0.0);
    if (tExit <= tEnter)
    {
        discard;
    }

    int steps = uQuality <= 0 ? 16 : (uQuality >= 2 ? 32 : CLOUD_STEPS);
    float stepLen = (tExit - tEnter) / float(steps);
    vec3 sunToward = normalize(-uSunDir);
    float cosTheta = dot(rd, sunToward);
    float miePhase = atmosphereMiePhase(cosTheta);
    float hgPhase = vcHGPhase(cosTheta, 0.72);
    int lightSteps = uQuality >= 2 ? 5 : 3;
    vec3 skyAmbient = sampleSkyAmbient(rd, uSkyViewLut, uHasSkyLut);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float jitter = fract(sin(dot(vUv + uFramePhase, vec2(12.9898, 78.233))) * 43758.5453) * stepLen;

    for (int i = 0; i < steps; ++i)
    {
        float t = tEnter + jitter + (float(i) + 0.5) * stepLen;
        vec3 worldPos = uCameraPos + rd * t;
        float density = vcCloudDensityEx(worldPos, uLayerHeight, layerTop, uDensity, uVolumeSize,
            uCloudNoise, uHasCloudNoise, uCoverageMap, uHasCoverageMap);
        if (density <= 1e-5)
        {
            continue;
        }

        float lightOd = vcLightOpticalDepth(worldPos, sunToward, uLayerHeight, layerTop, uDensity, uVolumeSize, lightSteps);
        float beerPowder = vcBeerPowder(lightOd);
        vec3 scatter = vcCloudScatterColor(uSunColor, miePhase, hgPhase, beerPowder, density);
        scatter += skyAmbient * density * 0.18;
        float inscatterW = vmSegmentInscatterWeight(density, stepLen, 1.1);
        accum += transmittance * scatter * inscatterW;
        transmittance *= vmSegmentTransmittance(density, stepLen, 1.1);
        if (transmittance < 0.03)
        {
            break;
        }
    }

    float alpha = saturate1(max(max(accum.r, accum.g), accum.b) * 0.72);
    if (alpha <= 0.012)
    {
        discard;
    }

    vec3 cloudCol = accum / (accum + vec3(0.42));

    if (uHasPrevClouds > 0 && uTemporalWeight > 0.0 && uGateSkyDepth > 0)
    {
        float depth = texture(uSceneDepth, vUv).r;
        vec2 ndc = vec2(vUv.x * 2.0 - 1.0, vUv.y * 2.0 - 1.0);
        float z = depth * 2.0 - 1.0;
        vec4 worldH = uInvViewProj * vec4(ndc, z, 1.0);
        vec3 worldPos = worldH.xyz / max(worldH.w, 1e-6);
        vec4 prevClip = uPrevViewProj * vec4(worldPos, 1.0);
        if (prevClip.w > 1e-6)
        {
            vec2 prevUv = prevClip.xy / prevClip.w * 0.5 + 0.5;
            if (prevUv.x >= 0.0 && prevUv.x <= 1.0 && prevUv.y >= 0.0 && prevUv.y <= 1.0)
            {
                vec4 hist = texture(uPrevClouds, prevUv);
                cloudCol = mix(cloudCol, hist.rgb, uTemporalWeight * hist.a);
                alpha = mix(alpha, hist.a, uTemporalWeight * 0.65);
            }
        }
    }

    FragColor = vec4(cloudCol, alpha);
}
