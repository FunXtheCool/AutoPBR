#version 330 core
in vec2 vUv;
uniform sampler2D uRays;
out vec4 FragColor;

void main()
{
    vec4 rays = texture(uRays, vUv);
    if (rays.a <= 1e-5)
    {
        discard;
    }

    FragColor = rays;
}
