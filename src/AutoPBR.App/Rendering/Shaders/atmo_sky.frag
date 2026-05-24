#version 330 core
//!include "common/common.glsl"

in vec2 vUv;
uniform sampler2D uSkyViewLut;
uniform vec3 uSunDir;
uniform float uSunIntensity;
out vec4 FragColor;

void main()
{
    vec3 sky = srgbToLinear(texture(uSkyViewLut, vUv).rgb);
    float sunMask = pow(max(dot(normalize(vec3(vUv.x * 2.0 - 1.0, vUv.y * 2.0 - 1.0, 1.0)), normalize(-uSunDir)), 0.0), 820.0);
    vec3 sun = vec3(1.0, 0.93, 0.74) * uSunIntensity * sunMask * 0.02;
    FragColor = vec4(linearToSrgb(sky + sun), 1.0);
}
