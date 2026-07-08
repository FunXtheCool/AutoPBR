// Genesis preview shader - directional shadow sampling helpers.
// Compatible with desktop GLSL 330 core and GLSL ES 300 (after GlslSourceAdapter).
// Hardware shadow comparison via sampler2DShadow + PCF, slope-scaled bias, and a
// manual border check (ES 300 has no CLAMP_TO_BORDER).

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

float shadowMapTexelDepth(vec2 shadowTexelSize)
{
    return max(max(shadowTexelSize.x, shadowTexelSize.y), 1.0 / 4096.0);
}

// minBias/maxBias are normalized-depth offsets; always enforce ~1.75 texels minimum so fitted
// subject ortho extents (large world span / 1024) do not acne-strip receivers.
float computeShadowBias(vec3 N, vec3 L, float minBias, float maxBias, vec2 shadowTexelSize)
{
    float ndl = clamp(dot(normalize(N), normalize(L)), 0.0, 1.0);
    float slope = 1.0 - ndl;
    float texel = shadowMapTexelDepth(shadowTexelSize);
    float slopeBias = maxBias * slope;
    float configured = max(minBias, slopeBias);
    return max(configured, texel * 1.75);
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

float sampleShadowBordered(sampler2DShadow shadowTex, vec3 shadowUv)
{
    if (shadowUv.x < 0.0 || shadowUv.x > 1.0 || shadowUv.y < 0.0 || shadowUv.y > 1.0)
    {
        return 1.0;
    }

    return texture(shadowTex, shadowUv);
}

float sampleShadowPcfSoft(sampler2DShadow shadowTex, vec3 shadowUv, vec2 texelSize, float softnessTexels)
{
    float radius = max(softnessTexels, 0.0);
    if (radius <= 1.0)
    {
        return sampleShadowPcf3x3(shadowTex, shadowUv, texelSize);
    }

    vec2 disk[16] = vec2[16](
        vec2(-0.942016, -0.399062), vec2( 0.945586, -0.768907),
        vec2(-0.094184, -0.929389), vec2( 0.344959,  0.293878),
        vec2(-0.915886,  0.457714), vec2(-0.815442, -0.879125),
        vec2(-0.382775,  0.276768), vec2( 0.974844,  0.756484),
        vec2( 0.443233, -0.975116), vec2( 0.537430, -0.473734),
        vec2(-0.264969, -0.418930), vec2( 0.791975,  0.190902),
        vec2(-0.241888,  0.997065), vec2(-0.814100,  0.914376),
        vec2( 0.199841,  0.786414), vec2( 0.143832, -0.141008)
    );

    float sum = sampleShadowBordered(shadowTex, shadowUv) * 2.0;
    float totalWeight = 2.0;
    for (int i = 0; i < 16; ++i)
    {
        vec2 off = disk[i] * texelSize * radius;
        sum += sampleShadowBordered(shadowTex, vec3(shadowUv.xy + off, shadowUv.z));
        totalWeight += 1.0;
    }

    return sum / totalWeight;
}

#endif // GENESIS_SHADOW_GLSL
