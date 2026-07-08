// Genesis preview shader - common helpers.
// Compatible with desktop GLSL 330 core and GLSL ES 300 (after GlslSourceAdapter).
// No #version directive here; only the entry .vert/.frag carries one.

#ifndef GENESIS_COMMON_GLSL
#define GENESIS_COMMON_GLSL

const float GEN_PI = 3.141592653589793;
const float GEN_INV_PI = 0.31830988618379067;
const float GEN_EPS = 1.0e-4;

float saturate1(float x) { return clamp(x, 0.0, 1.0); }
vec2  saturate2(vec2  x) { return clamp(x, vec2(0.0), vec2(1.0)); }
vec3  saturate3(vec3  x) { return clamp(x, vec3(0.0), vec3(1.0)); }
vec4  saturate4(vec4  x) { return clamp(x, vec4(0.0), vec4(1.0)); }

float pow5(float x)
{
    float x2 = x * x;
    return x2 * x2 * x;
}

// Manual sRGB encode used at end-of-frame because the preview FBO is not requested as sRGB
// (see GlPbrPreviewControl). Approximate gamma 2.2 is fine for a real-time preview.
vec3 linearToSrgb(vec3 c)
{
    return pow(saturate3(c), vec3(1.0 / 2.2));
}

// Decode an sRGB-encoded texel to linear; used when sampling diffuse/albedo.
vec3 srgbToLinear(vec3 c)
{
    return pow(saturate3(c), vec3(2.2));
}

// Breaks up 8-bit RGBA preview banding from tonemapped lighting gradients (FBO is not sRGB).
float interleavedGradientNoise(vec2 screenPos)
{
    return fract(52.9829189 * fract(dot(screenPos, vec2(0.06711056, 0.00583715))));
}

vec3 ditherSrgb8(vec3 srgb, vec2 screenPos)
{
    return saturate3(srgb + (interleavedGradientNoise(screenPos) - 0.5) / 255.0);
}

#endif // GENESIS_COMMON_GLSL
