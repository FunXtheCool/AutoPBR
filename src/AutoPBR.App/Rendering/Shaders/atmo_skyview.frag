#version 330 core
//!include "common/common.glsl"
//!include "common/atmosphere.glsl"
//!include "common/sky_dome.glsl"

in vec2 vUv;
uniform sampler2D uTransmittanceLut;
uniform vec3 uSunDir;
uniform float uTurbidity;
uniform float uSunIntensity;
uniform float uHorizonFalloff;
out vec4 FragColor;

void main()
{
    float viewZenith = vUv.y;
    float azimuth = vUv.x * 2.0 - 1.0;
    float sinTheta = sin(viewZenith * ATM_PI);
    vec3 viewDir = normalize(vec3(sinTheta * azimuth, cos(viewZenith * ATM_PI), sinTheta));
    float mu = clamp(viewDir.y, -1.0, 1.0);

    vec3 trans = srgbToLinear(texture(uTransmittanceLut, vec2(vUv.x, clamp(vUv.y, 0.0, 1.0))).rgb);
    float cosSun = dot(viewDir, normalize(-uSunDir));
    float rayleigh = atmosphereRayleighPhase(cosSun);
    float mie = atmosphereMiePhase(cosSun) * clamp(uTurbidity * 0.35, 0.2, 4.0);
    vec3 sunCol = atmosphereSunColor(uSunIntensity);

    float horizon = pow(1.0 - clamp(mu, 0.0, 1.0), max(uHorizonFalloff, 0.1));
    vec3 baseSky = mix(vec3(0.02, 0.04, 0.08), vec3(0.24, 0.43, 0.78), clamp(mu * 0.5 + 0.5, 0.0, 1.0));
    vec3 scatter = (atmosphereBetaRayleigh() * rayleigh * 120000.0 + vec3(mie * 0.045)) * sunCol;
    vec3 col = baseSky * mix(0.9, 0.45, horizon) + scatter * trans;

    float dayAmt = skyDayFactor(uSunDir, uSunIntensity);
    vec3 nightSky = skyNightZenith(viewDir);
    col = mix(nightSky, col, dayAmt);
    col = skySoftKnee(col, 0.12);
    FragColor = vec4(linearToSrgb(max(col, vec3(0.0))), 1.0);
}
