#version 330 core
// AutoPBR Genesis preview shader - depth-only shadow vertex stage.
// Same vertex layout as genesis.vert so the same VBO can be reused.

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aUv;
layout(location = 3) in vec4 aTangent;
layout(location = 4) in int aBoneIndex;

uniform mat4 uModel;
uniform mat4 uLightViewProj;

layout(std140) uniform EntitySkinningBones {
    mat4 uBoneMatrices[64];
    int uEntityGpuSkinning;
    int uEntityBoneCount;
    float uEntityMeshLiftY;
    int _entitySkinningPad0;
};

out vec2 vUv;

void main()
{
    vUv = aUv;
    vec4 entityPos;
    if (uEntityGpuSkinning != 0 && uEntityBoneCount > 0)
    {
        int bi = clamp(aBoneIndex, 0, uEntityBoneCount - 1);
        mat4 bone = uBoneMatrices[bi];
        entityPos = bone * vec4(aPos, 1.0);
        entityPos.xyz = entityPos.xyz / 16.0 - vec3(0.5);
        entityPos.y += uEntityMeshLiftY;
    }
    else
    {
        entityPos = vec4(aPos, 1.0);
    }

    gl_Position = uLightViewProj * uModel * entityPos;
}
