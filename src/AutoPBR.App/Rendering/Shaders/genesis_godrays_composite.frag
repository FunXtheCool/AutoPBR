#version 330 core
in vec2 vUv;
uniform sampler2D uRays;
out vec4 FragColor;

void main()
{
    FragColor = texture(uRays, vUv);
}
