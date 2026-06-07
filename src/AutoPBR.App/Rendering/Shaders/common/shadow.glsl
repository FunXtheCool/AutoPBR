// Genesis preview shader - directional shadow sampling helpers.
// Compatible with desktop GLSL 330 core and GLSL ES 300 (after GlslSourceAdapter).
// Hardware shadow comparison via sampler2DShadow + 3x3 PCF (9 taps), slope-scaled bias,
// and a manual border check (ES 300 has no CLAMP_TO_BORDER).

#ifndef GENESIS_SHADOW_GLSL
#define GENESIS_SHADOW_GLSL

//!include "common.glsl"

// Project world position into shadow UV. Returns xyz = UV+depth, w = inside frustum (1 = lit path).
vec4 worldToShadowUv(vec3 worldPos, mat4 lightVP)
{
    vec4 clip = lightVP * vec4(worldPos, 1.0);
    if (clip.w <= 0.0)
    {
        return vec4(0.0, 0.0, 0.0, 0.0);
    }

    vec3 ndc = clip.xyz / clip.w;
    vec3 uv = ndc * 0.5 + 0.5;

    float inside = 1.0;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || uv.z < 0.0 || uv.z > 1.0)
    {
        inside = 0.0;
    }

    return vec4(uv, inside);
}

float computeShadowBias(vec3 N, vec3 L, float minBias, float maxBias)
{
    float ndl = clamp(dot(normalize(N), normalize(L)), 0.0, 1.0);
    float slope = 1.0 - ndl;
    return clamp(maxBias * slope, minBias, maxBias);
}

float sampleShadowPcf3x3(sampler2DShadow shadowTex, vec3 shadowUv, vec2 texelSize)
{
    float sum = 0.0;
    for (int dy = -1; dy <= 1; ++dy)
    {
        for (int dx = -1; dx <= 1; ++dx)
        {
            vec2 off = vec2(float(dx), float(dy)) * texelSize;
            vec3 p = vec3(shadowUv.xy + off, shadowUv.z);
            sum += texture(shadowTex, p);
        }
    }

    return sum * (1.0 / 9.0);
}

#endif // GENESIS_SHADOW_GLSL
