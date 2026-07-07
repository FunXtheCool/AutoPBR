#version 330 core
// AutoPBR Genesis preview shader - vertex stage.
// Algorithms inspired by LabPBR 1.3 spec and Glimmer Shaders (MIT).

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aUv;
layout(location = 3) in vec4 aTangent;
layout(location = 4) in float aBoneIndexBits;

uniform mat4 uModel;
uniform mat4 uPrevModel;
uniform mat4 uView;
uniform mat4 uProj;
uniform mat4 uTaaCurrViewProj;
uniform mat4 uPrevViewProj;
uniform mat4 uLightViewProj;

// Bone palette only in UBO (std140 mat4[64]). Scalars are plain float uniforms - avoids std140 tail
// layout mismatches and GLES/ANGLE int-uniform quirks on Windows preview contexts.
layout(std140) uniform EntitySkinningBones {
    mat4 uBoneMatrices[64];
};

layout(std140) uniform EntityPrevSkinningBones {
    mat4 uPrevBoneMatrices[64];
};

uniform float uEntityPreviewSpaceVerts;
uniform float uEntityBindMesh;
uniform float uEntityGpuSkinning;
uniform float uEntityBoneCount;
uniform float uEntityMeshLiftY;
uniform float uEntityPrevBonePaletteValid;

out vec3 vWorldPos;
out vec3 vWorldNormal;
out vec2 vUv;
out vec4 vWorldTangent;
out vec4 vLightClip;
out vec4 vCurrClip;
out vec4 vPrevClip;

void main()
{
    vec4 entityPos;
    vec4 prevEntityPos;
    vec3 entityN;
    vec3 entityT;
    if (uEntityPreviewSpaceVerts > 0.5)
    {
        entityPos = vec4(aPos, 1.0);
        prevEntityPos = entityPos;
        entityN = aNormal;
        entityT = aTangent.xyz;
    }
    else if (uEntityBindMesh > 0.5)
    {
        entityPos = vec4(aPos, 1.0);
        if (uEntityGpuSkinning > 0.5)
        {
            int bi = clamp(floatBitsToInt(aBoneIndexBits), 0, int(uEntityBoneCount + 0.5) - 1);
            mat4 bone = uBoneMatrices[bi];
            entityPos = bone * entityPos;
            mat4 prevBone = uEntityPrevBonePaletteValid > 0.5 ? uPrevBoneMatrices[bi] : bone;
            prevEntityPos = prevBone * vec4(aPos, 1.0);
            mat3 nBone = mat3(transpose(inverse(bone)));
            entityN = normalize(nBone * aNormal * 16.0);
            entityT = normalize(nBone * aTangent.xyz * 16.0);
        }
        else
        {
            prevEntityPos = entityPos;
            entityN = normalize(aNormal * 16.0);
            entityT = normalize(aTangent.xyz * 16.0);
        }

        entityPos.xyz = entityPos.xyz / 16.0 - vec3(0.5);
        entityPos.y += uEntityMeshLiftY;
        prevEntityPos.xyz = prevEntityPos.xyz / 16.0 - vec3(0.5);
        prevEntityPos.y += uEntityMeshLiftY;
    }
    else
    {
        entityPos = vec4(aPos, 1.0);
        prevEntityPos = entityPos;
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
    vec4 clip = uProj * uView * wp;
    vCurrClip = uTaaCurrViewProj * wp;
    vPrevClip = uPrevViewProj * uPrevModel * prevEntityPos;
    gl_Position = clip;
}
