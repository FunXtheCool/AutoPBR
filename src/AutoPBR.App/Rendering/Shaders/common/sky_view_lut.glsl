// Shared sky-view LUT UV mapping and sampling (used by IBL and sky dome).

#ifndef GENESIS_SKY_VIEW_LUT_GLSL
#define GENESIS_SKY_VIEW_LUT_GLSL

const float SKY_VIEW_LUT_WIDTH = 192.0;

vec2 skyViewLutUv(vec3 viewDir)
{
    vec3 d = normalize(viewDir);
    float viewZenith = acos(clamp(d.y, -1.0, 1.0)) / GEN_PI;
    float u = atan(d.x, d.z) / (2.0 * GEN_PI) + 0.5;
    return vec2(u, viewZenith);
}

vec3 sampleSkyViewLutSrgb(sampler2D lut, vec3 viewDir)
{
    vec2 uv = skyViewLutUv(viewDir);
    float u = uv.x;
    float v = clamp(uv.y, 0.0, 1.0);
    float texelU = 1.0 / SKY_VIEW_LUT_WIDTH;
    vec3 c0 = texture(lut, vec2(u, v)).rgb;
    if (u < texelU)
    {
        vec3 c1 = texture(lut, vec2(u + 1.0, v)).rgb;
        c0 = mix(c1, c0, u / texelU);
    }
    else if (u > 1.0 - texelU)
    {
        vec3 c1 = texture(lut, vec2(u - 1.0, v)).rgb;
        c0 = mix(c0, c1, (u - (1.0 - texelU)) / texelU);
    }

    return c0;
}

#endif // GENESIS_SKY_VIEW_LUT_GLSL
