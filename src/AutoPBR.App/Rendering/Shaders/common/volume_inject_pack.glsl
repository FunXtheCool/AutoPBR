// Froxel inject output packing (ANGLE-safe: no vec constructors / swizzles in main).

#ifndef GENESIS_VOLUME_INJECT_PACK_GLSL
#define GENESIS_VOLUME_INJECT_PACK_GLSL

vec4 viPackFroxelInject(float mediumRho, vec3 lightColor, float shadowGate)
{
    float occ = step(GEN_EPS, mediumRho);
    vec3 sunLit = lightColor * mediumRho * shadowGate * 0.85;
    vec4 packed;
    packed.r = mediumRho;
    packed.g = sunLit.r;
    packed.b = sunLit.g;
    packed.a = occ;
    return packed;
}

#endif // GENESIS_VOLUME_INJECT_PACK_GLSL
