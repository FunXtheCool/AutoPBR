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

    vec3 trans = srgbToLinear(texture(uTransmittanceLut, vec2(vUv.x, clamp(vUv.y, 0.0, 1.0))).rgb);
    vec3 col = skyDayRadiance(viewDir, uSunDir, uSunIntensity, uTurbidity, uHorizonFalloff);
    col *= mix(vec3(1.0), trans + vec3(0.06), 0.35);

    float dayAmt = skyDayFactor(uSunDir, uSunIntensity);
    vec3 nightSky = skyNightZenith(viewDir);
    col = mix(nightSky, col, dayAmt);
    // Store untonemapped linear radiance (sRGB-encoded for 8-bit precision); the runtime
    // sky pass applies the single luminance tonemap. A knee here would double-compress.
    FragColor = vec4(linearToSrgb(clamp(col, vec3(0.0), vec3(1.0))), 1.0);
}
