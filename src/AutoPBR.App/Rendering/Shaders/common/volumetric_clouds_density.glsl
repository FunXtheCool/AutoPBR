// Procedural cloud density only (no ray march). Safe for GLES froxel inject includes.

#ifndef GENESIS_VOLUMETRIC_CLOUDS_DENSITY_GLSL
#define GENESIS_VOLUMETRIC_CLOUDS_DENSITY_GLSL

float vcHash31(vec3 p)
{
    p = fract(p * 0.3183099 + vec3(0.17, 0.31, 0.47));
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float vcNoise3(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = vcHash31(i + vec3(0.0, 0.0, 0.0));
    float n100 = vcHash31(i + vec3(1.0, 0.0, 0.0));
    float n010 = vcHash31(i + vec3(0.0, 1.0, 0.0));
    float n110 = vcHash31(i + vec3(1.0, 1.0, 0.0));
    float n001 = vcHash31(i + vec3(0.0, 0.0, 1.0));
    float n101 = vcHash31(i + vec3(1.0, 0.0, 1.0));
    float n011 = vcHash31(i + vec3(0.0, 1.0, 1.0));
    float n111 = vcHash31(i + vec3(1.0, 1.0, 1.0));

    float nx00 = mix(n000, n100, f.x);
    float nx10 = mix(n010, n110, f.x);
    float nx01 = mix(n001, n101, f.x);
    float nx11 = mix(n011, n111, f.x);
    float nxy0 = mix(nx00, nx10, f.y);
    float nxy1 = mix(nx01, nx11, f.y);
    return mix(nxy0, nxy1, f.z);
}

float vcFbm(vec3 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; ++i)
    {
        v += a * vcNoise3(p);
        p = p * 2.03 + vec3(1.7, 2.3, 0.9);
        a *= 0.5;
    }
    return v;
}

float vcCloudDensityRaw(vec3 worldPos, float layerBase, float layerTop, float densityMul, float volumeSize)
{
    if (worldPos.y < layerBase || worldPos.y > layerTop)
    {
        return 0.0;
    }

    float layerH = max(layerTop - layerBase, 0.001);
    float h = (worldPos.y - layerBase) / layerH;
    float heightFade = smoothstep(0.0, 0.12, h) * smoothstep(1.0, 0.58, h);

    float sizeScale = max(volumeSize, 8.0);
    vec3 samplePos = worldPos / sizeScale;
    float base = vcFbm(samplePos * 0.52 + vec3(0.0, h * 1.8, 0.0));
    float detail = 1.0 - vcFbm(samplePos * 1.75 + vec3(41.0, 17.0, 9.0));
    float coverage = saturate1((base * 0.62 + detail * 0.38 - 0.15) * 3.8 * densityMul);
    return coverage * heightFade;
}

#endif // GENESIS_VOLUMETRIC_CLOUDS_DENSITY_GLSL
