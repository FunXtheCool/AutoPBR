#version 330 core
// Additive screen-space god rays: visibility march toward the sun/moon disc.

//!include "common/common.glsl"

in vec2 vUv;
uniform sampler2D uSceneDepth;
uniform vec2 uSunUv;
uniform float uSunDiscRadius;
uniform float uSunConeRadius;
uniform float uStrength;

out vec4 FragColor;

const int GR_SAMPLES = 64;
const float SKY_DEPTH_EPS = 0.9992;

vec3 softKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

void main()
{
    if (uStrength <= 0.0)
    {
        discard;
    }

    float receiverDepth = texture(uSceneDepth, vUv).r;
    if (receiverDepth >= SKY_DEPTH_EPS)
    {
        discard;
    }

    vec2 toSun = uSunUv - vUv;
    float distFromSun = length(toSun);
    if (distFromSun > uSunConeRadius)
    {
        discard;
    }

    float shaft = 0.0;
    float visibility = 1.0;
    const float decay = 0.92;
    const float weight = 1.0 / float(GR_SAMPLES);

    for (int i = 0; i < GR_SAMPLES; ++i)
    {
        float t = float(i) / max(float(GR_SAMPLES - 1), 1.0);
        vec2 marchUv = mix(vUv, uSunUv, t);
        if (marchUv.x < 0.002 || marchUv.x > 0.998 || marchUv.y < 0.002 || marchUv.y > 0.998)
        {
            break;
        }

        float beamFalloff = 1.0 - smoothstep(uSunDiscRadius, uSunConeRadius, length(marchUv - uSunUv));
        if (beamFalloff <= 0.01)
        {
            visibility *= decay;
            continue;
        }

        float sampleDepth = texture(uSceneDepth, marchUv).r;
        float expectedDepth = mix(receiverDepth, SKY_DEPTH_EPS, t);
        if (sampleDepth < expectedDepth - 0.0006)
        {
            visibility *= 0.28;
            if (visibility < 0.04)
            {
                break;
            }

            continue;
        }

        if (sampleDepth >= SKY_DEPTH_EPS)
        {
            shaft += visibility * weight * beamFalloff;
        }

        visibility *= decay;
    }

    float sunProximity = 1.0 - smoothstep(uSunDiscRadius, uSunConeRadius, distFromSun);
    vec3 warmScatter = vec3(1.0, 0.94, 0.82);
    vec3 rays = warmScatter * shaft * sunProximity * uStrength * 4.5;
    rays = softKnee(rays, 0.45);

    float alpha = saturate1(max(max(rays.r, rays.g), rays.b));
    if (alpha <= 1e-5)
    {
        discard;
    }

    FragColor = vec4(rays, alpha);
}
