#version 330 core

// AutoPBR Genesis preview shader - depth-only shadow fragment stage.

// Item/sprite planes and entity emulated rigs sample albedo alpha so transparent texels do not write shadow depth.

// Color mask is disabled in BeginShadowPass; output satisfies core / ES 3.0 link rules.

layout(location = 0) out vec4 fragColor;

uniform sampler2D uAlbedo;

uniform int uSceneKind;

uniform float uAlphaCutoff;

uniform int uItemAlphaBlend;

uniform int uEntityAlphaMode;

in vec2 vUv;

void main()
{
    float aTex = texture(uAlbedo, vUv).a;

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
    else if (uEntityAlphaMode == 1)
    {
        if (aTex < uAlphaCutoff)
        {
            discard;
        }
    }
    else if (uEntityAlphaMode == 2)
    {
        float refBlend = max(uAlphaCutoff, 0.42);
        if (aTex < refBlend)
        {
            discard;
        }
    }

    fragColor = vec4(0.0);
}
