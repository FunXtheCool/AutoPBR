#version 330 core
#ifdef GENESIS_MATERIAL_DRAW_RECORD_SSBO
#extension GL_ARB_shader_storage_buffer_object : require
#endif
#define GENESIS_FRAGMENT_STAGE 1
//!include "common/genesis_draw_record.glsl"

// AutoPBR Genesis preview shader - depth-only shadow fragment stage.

// Item/sprite planes and entity emulated rigs sample albedo alpha so transparent texels do not write shadow depth.

// Color mask is disabled in BeginShadowPass; output satisfies core / ES 3.0 link rules.

layout(location = 0) out vec4 fragColor;

uniform sampler2D uAlbedo;
#ifdef GENESIS_MATERIAL_TEXTURE_ARRAYS
uniform sampler2DArray uAlbedoArray;
uniform int uGenesisUseMaterialTextureArray;
#endif

uniform int uSceneKind;

uniform float uAlphaCutoff;

uniform int uItemAlphaBlend;

uniform int uEntityAlphaMode;

in vec2 vUv;

float sampleShadowAlpha(vec2 uv)
{
#ifdef GENESIS_MATERIAL_TEXTURE_ARRAYS
    if (uGenesisUseMaterialTextureArray > 0)
    {
        return texture(uAlbedoArray, vec3(uv, float(genesisMaterialTextureLayer(0)))).a;
    }
#endif
    return texture(uAlbedo, uv).a;
}

void main()
{
    float aTex = sampleShadowAlpha(vUv);

    if (uSceneKind == 1)
    {
        float ref = uAlphaCutoff;
        if (uItemAlphaBlend >= 1)
        {
            ref = max(uAlphaCutoff, 0.42);
        }

        if (aTex < ref)
        {
            discard;
        }
    }
    else if (genesisEntityAlphaMode(uEntityAlphaMode) == 1)
    {
        if (aTex < uAlphaCutoff)
        {
            discard;
        }
    }
    else if (genesisEntityAlphaMode(uEntityAlphaMode) == 2)
    {
        float refBlend = max(uAlphaCutoff, 0.42);
        if (aTex < refBlend)
        {
            discard;
        }
    }

    fragColor = vec4(0.0);
}
