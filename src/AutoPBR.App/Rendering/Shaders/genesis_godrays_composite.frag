#version 330 core
in vec2 vUv;
uniform sampler2D uRays;
uniform sampler2D uCloudMask;
uniform int uHasCloudMask;
out vec4 FragColor;

void main()
{
    vec4 rays = texture(uRays, vUv);
    if (rays.a <= 1e-5)
    {
        discard;
    }

    if (uHasCloudMask > 0)
    {
        float cloudA = texture(uCloudMask, vUv).a;
        rays.rgb *= 1.0 - smoothstep(0.02, 0.14, cloudA);
        if (dot(rays.rgb, vec3(0.333333)) <= 1e-5)
        {
            discard;
        }
    }

    FragColor = rays;
}
