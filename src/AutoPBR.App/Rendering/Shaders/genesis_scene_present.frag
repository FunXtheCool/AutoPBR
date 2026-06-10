#version 330 core
// Copy offscreen scene color to the default framebuffer (god-ray capture present).

in vec2 vUv;
uniform sampler2D uSceneColor;
out vec4 FragColor;

void main()
{
    FragColor = texture(uSceneColor, vUv);
}
