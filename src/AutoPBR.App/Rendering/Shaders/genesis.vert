#version 330 core
#if defined(GENESIS_ENTITY_SKINNING_SSBO) || defined(GENESIS_MATERIAL_DRAW_RECORD_SSBO)
#extension GL_ARB_shader_storage_buffer_object : require
#endif
#ifdef GENESIS_DRAW_RECORD_BASE_INSTANCE
#extension GL_ARB_shader_draw_parameters : require
#endif
#define GENESIS_VERTEX_STAGE 1
//!include "common/genesis_draw_record.glsl"
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

// Bone palettes use SSBOs on capable desktop GL and UBOs everywhere else. Scalars stay as plain
// uniforms to preserve the GLES/ANGLE fallback and avoid std140 tail-layout quirks.
#ifdef GENESIS_ENTITY_SKINNING_SSBO
layout(std430, binding = 5) readonly buffer EntitySkinningBonesSsbo {
    mat4 uBoneMatrices[];
};

layout(std430, binding = 6) readonly buffer EntityPrevSkinningBonesSsbo {
    mat4 uPrevBoneMatrices[];
};

layout(std430, binding = 7) readonly buffer EntitySkinningNormalsSsbo {
    mat4 uNormalBoneMatrices[];
};
#else
layout(std140) uniform EntitySkinningBones {
    mat4 uBoneMatrices[64];
};

layout(std140) uniform EntityPrevSkinningBones {
    mat4 uPrevBoneMatrices[64];
};

layout(std140) uniform EntitySkinningNormals {
    mat4 uNormalBoneMatrices[64];
};
#endif

uniform float uEntityPreviewSpaceVerts;
uniform float uEntityBindMesh;
uniform float uEntityGpuSkinning;
uniform float uEntityBoneCount;
uniform float uEntityMeshLiftY;
uniform float uEntityPrevBonePaletteValid;
uniform vec2 uTextureAtlasScale;

out vec3 vWorldPos;
out vec3 vWorldNormal;
out vec2 vUv;
out vec4 vWorldTangent;
out vec4 vLightClip;
out vec4 vCurrClip;
out vec4 vPrevClip;

void main()
{
    genesisWriteDrawRecordIndexVarying();

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
            mat4 nBone = uNormalBoneMatrices[bi];
            entityN = normalize(mat3(nBone) * aNormal * 16.0);
            entityT = normalize(mat3(nBone) * aTangent.xyz * 16.0);
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
    vUv = aUv * genesisTextureAtlasScale(uTextureAtlasScale);
    vLightClip = uLightViewProj * wp;
    vec4 clip = uProj * uView * wp;
    vCurrClip = uTaaCurrViewProj * wp;
    vPrevClip = uPrevViewProj * uPrevModel * prevEntityPos;
    gl_Position = clip;
}
