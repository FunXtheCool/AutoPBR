// Froxel integrate helpers (entry-shader helpers belong here for ANGLE).

#ifndef GENESIS_VOLUME_INTEGRATE_HELPERS_GLSL
#define GENESIS_VOLUME_INTEGRATE_HELPERS_GLSL

vec3 viSoftKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

vec2 viReprojectUv(vec2 uv, float depth, mat4 invViewProj, mat4 prevViewProj)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    float z = depth * 2.0 - 1.0;
    vec4 worldH = invViewProj * vec4(ndc, z, 1.0);
    vec3 worldPos = worldH.xyz / max(worldH.w, 1e-6);
    vec4 prevClip = prevViewProj * vec4(worldPos, 1.0);
    if (prevClip.w <= 1e-6)
    {
        return vec2(-1.0);
    }

    vec2 prevNdc = prevClip.xy / prevClip.w;
    return prevNdc * 0.5 + 0.5;
}

#endif // GENESIS_VOLUME_INTEGRATE_HELPERS_GLSL
