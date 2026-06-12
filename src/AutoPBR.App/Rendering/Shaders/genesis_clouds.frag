#version 330 core
// Screen-space volumetric cloud layer (sky pixels only) with Beer-Powder lighting.

//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/sky_dome.glsl"
//!include "common/volumetric_clouds.glsl"
//!include "common/volumetric_medium.glsl"
//!include "common/volumetric_clouds_density_maps.glsl"
//!include "common/temporal_reproject.glsl"

in vec2 vUv;
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uSunDir;
uniform float uSunIntensity;
uniform float uSkyExposure;
uniform sampler2D uSkyViewLut;
uniform sampler3D uCloudNoise;
uniform sampler3D uDetailNoise;
uniform sampler2D uCoverageMap;
uniform sampler2D uSceneDepth;
uniform sampler2D uPrevClouds;
uniform mat4 uPrevViewProj;
uniform float uLayerHeight;
uniform float uVolumeHeight;
uniform float uDensity;
uniform float uCoverageScale;
uniform float uVolumeSize;
uniform vec3 uWindOffset;
uniform float uCirrusStrength;
uniform vec2 uCirrusWindOffset;
uniform int uQuality;
uniform int uMarchSteps;
uniform int uDebugView;
uniform int uGateSkyDepth;
uniform int uHasCloudNoise;
uniform int uHasDetailNoise;
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

// Ambient in-scatter from the sky dome, blended toward the night zenith as the sun sets.
// Uses the sky-view LUT's actual parameterization and decodes its sRGB storage; sampling
// is lifted above the horizon so the ambient reads the bright sky band, not the ground fog.
vec3 sampleSkyAmbient(vec3 rd, sampler2D skyLut, int hasSkyLut, float dayAmt)
{
    vec3 night = skyNightZenith(rd) * 2.0;
    if (hasSkyLut < 1)
    {
        return mix(night, vec3(0.42, 0.50, 0.63), dayAmt);
    }

    // Zenith-dominant sample: cloud ambient tracks the overhead sky brightness (dark at
    // dusk like the dome) while keeping some azimuthal sunset tint, instead of reading
    // the bright horizon band and glowing against a dark sky.
    vec3 ambientDir = normalize(vec3(rd.x * 0.35, max(rd.y, 0.45), rd.z * 0.35));
    vec3 lut = srgbToLinear(sampleSkyViewLutSrgb(skyLut, ambientDir));
    return mix(night, lut, dayAmt);
}

// Fade clouds toward the horizon and with distance so slabs do not sit on a hard line at the
// grid plane; grazing rays also lose opacity as the optical path lengthens.
float cloudHorizonLifetime(vec3 rd, float slabEnter)
{
    float elevFade = smoothstep(0.04, 0.2, rd.y);
    float grazingFade = 1.0 - smoothstep(0.48, 0.9, 1.0 - rd.y);
    float distFade = 1.0 - smoothstep(50.0, 220.0, max(slabEnter, 0.0));
    return elevFade * grazingFade * distFade;
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
    bool slabHit = tExit > tEnter;
    bool hasCumulus = slabHit;
    vec3 cloudCol = vec3(0.0);
    float alpha = 0.0;
    bool debugViewActive = false;

    if (uDebugView == 1 && slabHit)
    {
        float tMid = (tEnter + tExit) * 0.5;
        vec3 pos = uCameraPos + rd * tMid;
        vec2 weather = vcSampleWeather(uCoverageMap, uHasCoverageMap, pos, uVolumeSize, uWindOffset.xz);
        float cov = saturate1(weather.x * uCoverageScale);
        cloudCol = vec3(cov, weather.y, 0.35);
        alpha = cov > 0.02 ? 0.95 : 0.0;
        debugViewActive = true;
    }
    else if (uDebugView == 2 && rd.y > 0.02)
    {
        float sliceY = uLayerHeight + uVolumeHeight * 0.5;
        float tSlice = (sliceY - uCameraPos.y) / max(rd.y, 1e-4);
        if (tSlice > 0.0)
        {
            vec3 pos = uCameraPos + rd * tSlice;
            float density = vcCloudDensityEx(pos, uLayerHeight, layerTop, uDensity, uCoverageScale, uVolumeSize,
                uCloudNoise, uHasCloudNoise, uDetailNoise, uHasDetailNoise, uCoverageMap, uHasCoverageMap, uWindOffset);
            cloudCol = vec3(density * 2.8, density * 1.4, density * 0.35);
            alpha = saturate1(density * 3.5);
            debugViewActive = true;
        }
    }

    if (!debugViewActive)
    {
    if (hasCumulus)
    {
        // Coverage pre-test: a few cheap weather-map taps along the segment let fully clear
        // rays skip the march (and all of its 3D texture work) entirely.
        float covMax = 0.0;
        for (int i = 0; i < 6; ++i)
        {
            float tCov = mix(tEnter, tExit, (float(i) + 0.5) / 6.0);
            vec3 covPos = uCameraPos + rd * tCov;
            covMax = max(covMax, vcSampleWeather(uCoverageMap, uHasCoverageMap, covPos, uVolumeSize, uWindOffset.xz).x);
        }

        hasCumulus = covMax * uCoverageScale > 1e-3;
    }

    vec3 sunToward = normalize(-uSunDir);
    float cosTheta = dot(rd, sunToward);
    float dayAmt = skyDayFactor(uSunDir, uSunIntensity);
    vec3 sunColor = vcCloudSunColor(sunToward, uSunIntensity);
    vec3 skyAmbient = sampleSkyAmbient(rd, uSkyViewLut, uHasSkyLut, dayAmt);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float lifetime = cloudHorizonLifetime(rd, tEnter);

    if (hasCumulus)
    {
        int steps = uMarchSteps > 0 ? uMarchSteps : (uQuality <= 0 ? 16 : (uQuality >= 2 ? 32 : CLOUD_STEPS));
        float stepLen = (tExit - tEnter) / float(steps);
        int lightSteps = uQuality >= 2 ? 5 : 3;
        // Interleaved gradient noise: spatially even (blue-noise-like) error distribution, so
        // step banding dissolves into fine grain instead of the streaks white-noise hashing
        // produces; the frame phase slides the pattern so temporal blending averages it out.
        vec2 ignCoord = gl_FragCoord.xy + uFramePhase * vec2(47.0, 17.0);
        float jitter = fract(52.9829189 * fract(dot(ignCoord, vec2(0.06711056, 0.00583715)))) * stepLen;

        for (int i = 0; i < 32; ++i)
        {
            if (i >= steps)
            {
                break;
            }

            float t = tEnter + jitter + (float(i) + 0.5) * stepLen;
            vec3 worldPos = uCameraPos + rd * t;
            float density = vcCloudDensityEx(worldPos, uLayerHeight, layerTop, uDensity, uCoverageScale, uVolumeSize,
                uCloudNoise, uHasCloudNoise, uDetailNoise, uHasDetailNoise, uCoverageMap, uHasCoverageMap, uWindOffset);
            density *= lifetime;
            if (density <= 1e-5)
            {
                continue;
            }

            float lightOd = vcLightOpticalDepth(worldPos, sunToward, uLayerHeight, layerTop, uDensity, uCoverageScale,
                uVolumeSize, lightSteps, uCloudNoise, uHasCloudNoise, uCoverageMap, uHasCoverageMap, uWindOffset);
            // Segment radiance: multi-scatter sun term + height-graded sky ambient (lit tops,
            // shadowed bases). Density/extinction is applied once via the in-scatter weight
            // and transmittance product below.
            float hSample = saturate1((worldPos.y - uLayerHeight) / max(uVolumeHeight, 0.001));
            vec3 radiance = vcSunScatter(sunColor, cosTheta, lightOd);
            radiance += skyAmbient * mix(0.35, 1.0, hSample) * 0.72;
            float inscatterW = vmSegmentInscatterWeight(density, stepLen, 1.1);
            accum += transmittance * radiance * inscatterW;
            transmittance *= vmSegmentTransmittance(density, stepLen, 1.1);
            if (transmittance < 0.03)
            {
                break;
            }
        }
    }

    // High thin cirrus sheet above the cumulus slab: one plane intersection, lit with the
    // same sun/ambient terms, composited behind the march through its remaining transmittance.
    float cirrusY = layerTop + max(uVolumeHeight * 1.5, 18.0);
    if (uCirrusStrength > 0.0 && uCameraPos.y < cirrusY)
    {
        float tCirrus = (cirrusY - uCameraPos.y) / max(rd.y, 1e-4);
        vec3 cirrusPos = uCameraPos + rd * tCirrus;
        float cirrusDensity = vcCirrusDensity(cirrusPos.xz, uCirrusWindOffset, uVolumeSize);
        cirrusDensity *= cloudHorizonLifetime(rd, tCirrus);
        if (cirrusDensity > 1e-3)
        {
            // Grazing rays cross more of the thin sheet; fade slant boost near the horizon.
            float slant = mix(1.0, clamp(1.0 / max(rd.y, 0.18), 1.0, 2.4), lifetime);
            float cirrusAlpha = saturate1(cirrusDensity * uCirrusStrength * 0.5 * slant);
            // Thin ice layer: negligible self-shadowing, strong forward scatter, bright ambient.
            vec3 cirrusRad = vcSunScatter(sunColor, cosTheta, cirrusDensity * 1.6) + skyAmbient * 0.7;
            accum += transmittance * cirrusRad * cirrusAlpha;
            transmittance *= 1.0 - cirrusAlpha;
        }
    }

    // Skylight fill along clear segments of the ray so gaps between puffs read as bright air,
    // not void; keeps daylight gaps from showing night zenith / stars through low-alpha haze.
    float clearAmt = (1.0 - transmittance) * mix(0.35, 0.55, dayAmt);
    accum += skyAmbient * clearAmt;

    // Opacity comes from optical depth, not scattered luminance: a thick cloud occludes the
    // sky even when its in-scatter toward the eye is dim. Lifetime fade dissolves the deck
    // before the horizon line instead of stacking clouds on the grid plane.
    alpha = saturate1(1.0 - transmittance) * lifetime;

    // Match the sky pass pipeline (exposure -> soft knee -> sRGB) so clouds composite
    // seamlessly over the dome instead of reading darker than the sky behind them.
    cloudCol = linearToSrgb(skySoftKnee(accum * uSkyExposure, 0.08));

    if (uHasPrevClouds > 0 && uTemporalWeight > 0.0)
    {
        // Reproject by the cloud layer's own distance, not scene depth: sky depth sits at the
        // far plane, which under camera translation yields the parallax of an infinitely far
        // point and smears history sideways relative to a slab only tens of units away.
        float tRep = slabHit ? (tEnter + tExit) * 0.5 : (cirrusY - uCameraPos.y) / max(rd.y, 1e-4);
        vec3 repPoint = uCameraPos + rd * max(tRep, 0.0);
        vec2 prevUv = trReprojectUvFromWorld(repPoint, uPrevViewProj);
        if (trPrevUvOnScreen(prevUv))
        {
            vec2 velocity = vUv - prevUv;
            float motionW = trMotionRejectionWeight(velocity, 0.001, 0.04);
            float borderW = trHistoryBorderWeight(prevUv, 0.03);
            vec4 hist = texture(uPrevClouds, prevUv);
            float blend = uTemporalWeight * hist.a * motionW * borderW;
            cloudCol = mix(cloudCol, hist.rgb, blend);
            alpha = mix(alpha, hist.a, uTemporalWeight * 0.65 * motionW * borderW);
        }
    }
    }

    if (alpha <= 0.035)
    {
        discard;
    }

    FragColor = vec4(cloudCol, alpha);
}
