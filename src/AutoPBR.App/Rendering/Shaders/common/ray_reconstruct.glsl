// View ray reconstruction (no shadow / cloud density includes).

#ifndef GENESIS_RAY_RECONSTRUCT_GLSL
#define GENESIS_RAY_RECONSTRUCT_GLSL

vec3 grWorldPosFromUvDepth(vec2 uv, float depth, mat4 invViewProj)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    float z = depth * 2.0 - 1.0;
    vec4 worldH = invViewProj * vec4(ndc, z, 1.0);
    return worldH.xyz / max(worldH.w, 1e-6);
}

vec3 grWorldRayDir(vec2 uv, mat4 invViewProj, vec3 cameraPos)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    vec4 worldH = invViewProj * vec4(ndc, 1.0, 1.0);
    vec3 farPt = worldH.xyz / max(worldH.w, 1e-6);
    vec3 rd = farPt - cameraPos;
    float len2 = max(dot(rd, rd), 1e-12);
    return rd * inversesqrt(len2);
}

#endif // GENESIS_RAY_RECONSTRUCT_GLSL
