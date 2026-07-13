// Froxel inject output packing.
// GLES/ANGLE: component writes (no swizzles in vec4() constructors; avoid identifier "packed").
// Desktop GL: direct vec4() return.

#ifndef GENESIS_VOLUME_INJECT_PACK_GLSL
#define GENESIS_VOLUME_INJECT_PACK_GLSL

vec4 viPackFroxelInject(float mediumRho, vec3 lightColor, float shadowGate)
{
    float occ = step(GEN_EPS, mediumRho);
    vec3 sunLit = lightColor * mediumRho * shadowGate * 0.85;
#ifdef GENESIS_GLES
    vec4 injectOut;
    injectOut.r = mediumRho;
    injectOut.g = sunLit.r;
    injectOut.b = sunLit.g;
    injectOut.a = occ;
    return injectOut;
#else
    return vec4(mediumRho, sunLit.x, sunLit.y, occ);
#endif
}

#endif // GENESIS_VOLUME_INJECT_PACK_GLSL
