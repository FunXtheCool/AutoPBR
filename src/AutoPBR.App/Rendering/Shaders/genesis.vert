#version 330 core
// AutoPBR Genesis preview shader - vertex stage.
// Algorithms inspired by LabPBR 1.3 spec and Glimmer Shaders (MIT).

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aUv;
layout(location = 3) in vec4 aTangent;
layout(location = 4) in int aBoneIndex;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
uniform mat4 uLightViewProj;

// Bone palette + skinning scalars in one std140 UBO (binding set from API; see OpenGlPreviewBackend).
layout(std140) uniform EntitySkinningBones {
    mat4 uBoneMatrices[64];
    int uEntityGpuSkinning;
    int uEntityBoneCount;
    float uEntityMeshLiftY;
    int _entitySkinningPad0;
};

out vec3 vWorldPos;
out vec3 vWorldNormal;
out vec2 vUv;
out vec4 vWorldTangent;
out vec4 vLightClip;

void main()
{
    vec4 entityPos;
    vec3 entityN;
    vec3 entityT;
    if (uEntityGpuSkinning != 0 && uEntityBoneCount > 0)
    {
        int bi = clamp(aBoneIndex, 0, uEntityBoneCount - 1);
        mat4 bone = uBoneMatrices[bi];
        entityPos = bone * vec4(aPos, 1.0);
        // Match MinecraftModelBaker W(): texel model space -> Genesis preview unit box (CPU baker applies this after LocalToParent).
        entityPos.xyz = entityPos.xyz / 16.0 - vec3(0.5);
        entityPos.y += uEntityMeshLiftY;
        mat3 nBone = mat3(transpose(inverse(mat4(bone))));
        entityN = normalize(nBone * aNormal * 16.0);
        entityT = normalize(nBone * aTangent.xyz * 16.0);
    }
    else
    {
        entityPos = vec4(aPos, 1.0);
        entityN = aNormal;
        entityT = aTangent.xyz;
    }

    vec4 wp = uModel * entityPos;
    vWorldPos = wp.xyz;
    mat3 m3 = mat3(uModel);
    vWorldNormal = m3 * entityN;
    vec3 t = m3 * entityT;
    vWorldTangent = vec4(normalize(t), aTangent.w);
    vUv = aUv;
    vLightClip = uLightViewProj * wp;
    gl_Position = uProj * uView * wp;
}
