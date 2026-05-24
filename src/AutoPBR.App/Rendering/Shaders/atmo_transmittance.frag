#version 330 core
//!include "common/common.glsl"
//!include "common/atmosphere.glsl"

in vec2 vUv;
uniform float uTurbidity;
uniform float uHorizonFalloff;
out vec4 FragColor;

void main()
{
    float mu = clamp(vUv.y * 2.0 - 1.0, -1.0, 1.0);
    float horizon = pow(1.0 - clamp(mu, 0.0, 1.0), max(uHorizonFalloff, 0.1));
    float densityR = mix(0.85, 1.6, horizon);
    float densityM = mix(0.35, 2.1, horizon) * clamp(uTurbidity / 3.0, 0.2, 4.0);
    vec3 tau = atmosphereBetaRayleigh() * densityR + vec3(2.0e-5) * densityM;
    vec3 trans = exp(-tau * 8500.0);
    FragColor = vec4(linearToSrgb(clamp(trans, 0.0, 1.0)), 1.0);
}
