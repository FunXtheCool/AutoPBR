#version 330 core
//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/sky_dome.glsl"
//!include "common/godray_integration.glsl"

in vec2 vUv;
uniform sampler2D uSkyViewLut;
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform float uSunIntensity;
uniform float uHorizonFogStrength;
uniform float uSkyExposure;
uniform float uSunDiscStrength;
uniform float uSunCosDiscEdge;
uniform float uMoonCosDiscEdge;
uniform float uRenderTime;
uniform float uViewportAspect;
uniform float uTurbidity;
uniform float uHorizonFalloff;
uniform int uLutValid;
uniform float uSunDiscRadiusUv;
out vec4 FragColor;

// Matches atmo_skyview.frag (linear RGB) when the LUT bake is unavailable on GLES.
vec3 skyProceduralFromSun(vec3 viewDir, vec3 lightPropagationDir, float sunIntensity)
{
    float mu = clamp(viewDir.y, -1.0, 1.0);
    float cosSun = dot(viewDir, normalize(-lightPropagationDir));
    float rayleigh = atmosphereRayleighPhase(cosSun);
    float mie = atmosphereMiePhase(cosSun) * clamp(uTurbidity * 0.35, 0.2, 4.0);
    vec3 sunCol = atmosphereSunColor(sunIntensity);
    float horizon = pow(1.0 - clamp(mu, 0.0, 1.0), max(uHorizonFalloff, 0.1));
    vec3 baseSky = mix(vec3(0.02, 0.04, 0.08), vec3(0.24, 0.43, 0.78), clamp(mu * 0.5 + 0.5, 0.0, 1.0));
    vec3 scatter = (atmosphereBetaRayleigh() * rayleigh * 120000.0 + vec3(mie * 0.045)) * sunCol;
    vec3 col = baseSky * mix(0.9, 0.45, horizon) + scatter;
    float dayAmt = skyDayFactor(lightPropagationDir, sunIntensity);
    vec3 nightSky = skyNightZenith(viewDir);
    col = mix(nightSky, col, dayAmt);
    return max(col, vec3(0.0));
}

void main()
{
    vec3 viewDir = grWorldRayDir(vUv, uInvViewProj, uCameraPos);
    float dayAmt = skyDayFactor(uLightDir, uSunIntensity);

    vec3 lutSky;
    if (uLutValid > 0)
    {
        vec2 lutUv = skyViewLutUv(viewDir);
        lutSky = srgbToLinear(texture(uSkyViewLut, lutUv).rgb);
        if (dot(lutSky, vec3(0.333333)) < 1e-4)
        {
            lutSky = skyProceduralFromSun(viewDir, uLightDir, uSunIntensity);
        }
    }
    else
    {
        lutSky = skyProceduralFromSun(viewDir, uLightDir, uSunIntensity);
    }

    vec3 nightSky = skyNightZenith(viewDir) + skyStars(viewDir, uRenderTime);
    vec3 sunTint = vec3(1.0, 0.93, 0.74);

    vec3 sky = mix(nightSky, lutSky, dayAmt);
    sky += skyHorizonGlow(viewDir, dayAmt, sunTint);
    sky += skyBelowHorizonFog(viewDir, uHorizonFogStrength);

    if (dayAmt > 0.12)
    {
        sky += sunTint * skySunDiscBloom(viewDir, uLightDir, uSunCosDiscEdge, uSunDiscRadiusUv, uSunDiscStrength) * dayAmt;
    }

    float nightAmt = 1.0 - dayAmt;
    if (nightAmt > 0.3)
    {
        sky += skyMoonDiscShading(viewDir, uLightDir, uMoonCosDiscEdge) * nightAmt;
    }

    sky *= uSkyExposure;
    sky = skySoftKnee(sky, 0.08);
    FragColor = vec4(linearToSrgb(max(sky, vec3(0.0))), 1.0);
}
