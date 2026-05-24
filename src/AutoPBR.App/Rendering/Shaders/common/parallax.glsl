// Genesis preview shader - Parallax Occlusion Mapping with binary refinement and self-shadow.
// Algorithmic pattern inspired by Glimmer Shaders (MIT, https://github.com/jbritain/glimmer-shaders)
// and the LabPBR community POM literature; expressed for a single-pass forward preview without
// access to a depth buffer.

#ifndef GENESIS_PARALLAX_GLSL
#define GENESIS_PARALLAX_GLSL

//!include "common.glsl"

const int GEN_POM_TRACE_LAYERS = 40;
const int GEN_POM_REFINE_STEPS = 6;
const int GEN_POM_SHADOW_TAPS = 16;
const int GEN_POM_AO_TAPS = 8;
const float GEN_POM_MAX_UV_SHIFT = 0.22;
// Below this tangent-space view Z (cos angle to normal), POM is unstable / silhouette - skip.
const float GEN_POM_MIN_VIEW_Z = 0.055;
// Soft cap on |tan(theta)| = |V.xy|/V.z before parallax scale (preserves direction; avoids bent rays).
const float GEN_POM_STEER_TAN_CAP = 6.0;

// Preview meshes use GL_REPEAT; sampling height outside [0,1] during POM wraps to the opposite edge and
// reads the wrong texels on single-tile UVs (cube/item). Clamp UV for height reads only.
float sampleHeight01(sampler2D heightTex, vec2 uv)
{
    vec2 uvc = clamp(uv, vec2(0.0), vec2(1.0));
    return 1.0 - texture(heightTex, uvc).r;
}

// Matches UV displacement scale used for the view-ray march (also applied to parallax self-shadow).
float pomUvDisplacementScale(float strength)
{
    return clamp(strength, 0.0, 0.35) * 0.92;
}

// Trace POM from sampled height map. Returns the displaced UV; writes the surface depth at that hit
// (0 at the surface, 1 fully embedded) so the self-shadow trace can start from the correct layer.
//   uv0      : original surface UV
//   Vtan     : view direction in tangent space (normalized)
//   strength : height scale in 0..1 of layer thickness
vec2 traceParallaxPom(sampler2D heightTex, vec2 uv0, vec3 Vtan, float strength, out float hitDepth)
{
    hitDepth = 0.0;
    if (strength <= 0.0)
    {
        return uv0;
    }

    // Front-facing hemisphere only (view exits +N side). Using abs(z) or bending xy/z breaks coherent motion.
    if (Vtan.z < GEN_POM_MIN_VIEW_Z)
    {
        return uv0;
    }

    // Fixed layer count to avoid angle-driven popping/swimming from per-frame layer quantization.
    int layers = GEN_POM_TRACE_LAYERS;
    float layerStep = 1.0 / float(GEN_POM_TRACE_LAYERS);

    // Classic parallax ray on the tangent plane: delta_uv proportional to V.xy/V.z (unnormalized steer ok up to cap).
    vec2 steer = Vtan.xy / max(Vtan.z, GEN_EPS);
    float tanMag = length(steer);
    if (tanMag > GEN_POM_STEER_TAN_CAP)
    {
        steer *= GEN_POM_STEER_TAN_CAP / max(tanMag, GEN_EPS);
    }

    float parallaxScale = pomUvDisplacementScale(strength);
    vec2 totalOffset = steer * parallaxScale;
    float totalLen = length(totalOffset);
    if (totalLen > GEN_POM_MAX_UV_SHIFT)
    {
        totalOffset *= GEN_POM_MAX_UV_SHIFT / max(totalLen, GEN_EPS);
    }

    vec2 deltaUv = totalOffset * layerStep;

    vec2  curUv  = uv0;
    float curLayer = 0.0;
    float curHeightSample = sampleHeight01(heightTex, curUv);

    // Linear march until ray depth crosses sampled height.
    int marchSteps = 0;
    for (int i = 0; i < GEN_POM_TRACE_LAYERS; ++i)
    {
        if (curLayer >= curHeightSample)
        {
            break;
        }

        curUv -= deltaUv;
        curLayer += layerStep;
        marchSteps++;
        curHeightSample = sampleHeight01(heightTex, curUv);
    }

    // Binary refinement only after at least one linear step. Zero-step hits sit exactly on the surface;
    // refinement used prevLayer < 0 and moved UV away from the correct texel (visible smear on peaks).
    if (marchSteps > 0)
    {
        vec2  prevUv    = curUv + deltaUv;
        float prevLayer = curLayer - layerStep;

        for (int i = 0; i < GEN_POM_REFINE_STEPS; ++i)
        {
            vec2  midUv    = 0.5 * (prevUv + curUv);
            float midLayer = 0.5 * (prevLayer + curLayer);
            float midH     = sampleHeight01(heightTex, midUv);
            if (midLayer >= midH)
            {
                curUv = midUv;
                curLayer = midLayer;
                curHeightSample = midH;
            }
            else
            {
                prevUv = midUv;
                prevLayer = midLayer;
            }
        }
    }

    hitDepth = clamp(curLayer, 0.0, 1.0);
    return curUv;
}

// Cheap parallax self-shadow: march toward the light from the hit point and accumulate the
// largest height-above-ray difference. Returns 1.0 (lit) .. 0.0 (fully shadowed); already gated
// to 1.0 if the light is below the surface in tangent space.
float traceParallaxShadow(sampler2D heightTex, vec2 uvHit, vec3 Ltan, float refDepth, float strength)
{
    if (Ltan.z <= 0.0 || strength <= 0.0)
    {
        return 1.0;
    }

    float lz = max(Ltan.z, GEN_EPS);
    int   taps = GEN_POM_SHADOW_TAPS;
    float stepLen = refDepth / float(taps);
    if (stepLen <= 0.0)
    {
        return 1.0;
    }

    float uvScale = pomUvDisplacementScale(strength);
    vec2  uvStep = (Ltan.xy / lz) * uvScale * stepLen;
    vec2  uv = uvHit;
    float curLayer = refDepth;
    float maxOcclusion = 0.0;

    for (int i = 0; i < GEN_POM_SHADOW_TAPS; ++i)
    {
        if (i >= taps || curLayer <= 0.0)
        {
            break;
        }

        uv += uvStep;
        curLayer -= stepLen;
        float sampleH = sampleHeight01(heightTex, uv);
        if (curLayer > sampleH)
        {
            // Ray is above the surface here; no occluder.
            continue;
        }

        float delta = sampleH - curLayer;
        maxOcclusion = max(maxOcclusion, delta);
    }

    return clamp(1.0 - maxOcclusion * 6.25, 0.0, 1.0);
}

// Contact AO from local height neighborhood around the POM hit point.
// This targets the "grounded" crevice darkening many packs pair with POM.
float traceParallaxAo(sampler2D heightTex, vec2 uvHit, float refDepth, float strength, float aoStrength)
{
    if (refDepth <= 0.0 || strength <= 0.0 || aoStrength <= 0.0)
    {
        return 1.0;
    }

    float uvScale = pomUvDisplacementScale(strength);
    float radius = uvScale * mix(0.22, 0.72, clamp(refDepth, 0.0, 1.0));
    if (radius <= GEN_EPS)
    {
        return 1.0;
    }

    vec2 tapDirs[GEN_POM_AO_TAPS] = vec2[](
        vec2(1.0, 0.0), vec2(-1.0, 0.0), vec2(0.0, 1.0), vec2(0.0, -1.0),
        vec2(0.7071, 0.7071), vec2(-0.7071, 0.7071), vec2(0.7071, -0.7071), vec2(-0.7071, -0.7071)
    );

    float occ = 0.0;
    for (int i = 0; i < GEN_POM_AO_TAPS; ++i)
    {
        float ring = (float(i) + 1.0) / float(GEN_POM_AO_TAPS);
        vec2 uv = uvHit + tapDirs[i] * radius * ring;
        float sampleH = sampleHeight01(heightTex, uv);
        occ += max(0.0, sampleH - refDepth);
    }

    occ /= float(GEN_POM_AO_TAPS);
    // AO should mostly affect indirect/cavity lighting; keep it subtle.
    return clamp(1.0 - occ * 2.7 * aoStrength, 0.55, 1.0);
}

#endif // GENESIS_PARALLAX_GLSL
