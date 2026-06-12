#version 330 core
// Depth-aware upsample of the half-res cloud target onto the full-res frame.
// Clouds exist only on sky pixels: geometry pixels discard outright (so the half-res fetch
// can never smear cloud haze across silhouettes), and sky pixels renormalize the 4-tap blend
// by alpha so silhouette-adjacent discarded texels do not drag cloud edges dark.

in vec2 vUv;
uniform sampler2D uClouds;
uniform sampler2D uSceneDepth;
uniform vec2 uCloudTexelSize;
uniform int uHasSceneDepth;
out vec4 FragColor;

const float SKY_DEPTH_EPS = 0.9992;

void main()
{
    if (uHasSceneDepth > 0 && texture(uSceneDepth, vUv).r < SKY_DEPTH_EPS)
    {
        discard;
    }

    vec2 o = uCloudTexelSize * 0.5;
    vec4 c0 = texture(uClouds, vUv + vec2(-o.x, -o.y));
    vec4 c1 = texture(uClouds, vUv + vec2(o.x, -o.y));
    vec4 c2 = texture(uClouds, vUv + vec2(-o.x, o.y));
    vec4 c3 = texture(uClouds, vUv + vec2(o.x, o.y));

    float coverage = (c0.a + c1.a + c2.a + c3.a) * 0.25;
    if (coverage <= 0.03)
    {
        discard;
    }

    // Alpha-weighted color: texels the cloud pass discarded hold (0,0,0,0) and would
    // otherwise darken the blend; weighting recovers the true cloud color at soft edges.
    float wSum = c0.a + c1.a + c2.a + c3.a;
    vec3 rgb = (c0.rgb * c0.a + c1.rgb * c1.a + c2.rgb * c2.a + c3.rgb * c3.a) / max(wSum, 1e-5);
    FragColor = vec4(rgb, coverage);
}
