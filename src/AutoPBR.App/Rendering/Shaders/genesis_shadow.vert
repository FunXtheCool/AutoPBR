#version 330 core

// AutoPBR Genesis preview shader - depth-only shadow vertex stage.

// Same vertex layout as genesis.vert so the same VBO can be reused.



layout(location = 0) in vec3 aPos;

layout(location = 1) in vec3 aNormal;

layout(location = 2) in vec2 aUv;

layout(location = 3) in vec4 aTangent;

layout(location = 4) in float aBoneIndexBits;



uniform mat4 uModel;

uniform mat4 uLightViewProj;



layout(std140) uniform EntitySkinningBones {

    mat4 uBoneMatrices[64];

};



uniform float uEntityPreviewSpaceVerts;
uniform float uEntityBindMesh;
uniform float uEntityGpuSkinning;
uniform float uEntityBoneCount;
uniform float uEntityMeshLiftY;



out vec2 vUv;



void main()

{

    vUv = aUv;

    vec4 entityPos;

    if (uEntityPreviewSpaceVerts > 0.5)

    {

        entityPos = vec4(aPos, 1.0);

    }

    else if (uEntityBindMesh > 0.5)
    {
        entityPos = vec4(aPos, 1.0);
        if (uEntityGpuSkinning > 0.5)

        {

            int bi = clamp(floatBitsToInt(aBoneIndexBits), 0, int(uEntityBoneCount + 0.5) - 1);

            mat4 bone = uBoneMatrices[bi];

            entityPos = bone * entityPos;

        }



        entityPos.xyz = entityPos.xyz / 16.0 - vec3(0.5);

        entityPos.y += uEntityMeshLiftY;

    }

    else

    {

        entityPos = vec4(aPos, 1.0);

    }



    gl_Position = uLightViewProj * uModel * entityPos;

}

