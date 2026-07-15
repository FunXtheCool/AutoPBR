// Genesis preview shader - Parallax Occlusion Mapping with binary refinement and self-shadow.
// Algorithmic pattern inspired by Glimmer Shaders (MIT, https://github.com/jbritain/glimmer-shaders)
// and the LabPBR community POM literature; expressed for a single-pass forward preview without
// access to a depth buffer.

#ifndef GENESIS_PARALLAX_GLSL
#define GENESIS_PARALLAX_GLSL

//!include "common.glsl"

#ifndef GENESIS_DRAW_RECORD_GLSL
float genesisParallaxUvScale(float fallbackValue)
{
    return fallbackValue;
}

vec2 genesisParallaxHeightTexSize(vec2 fallbackValue)
{
    return fallbackValue;
}
#endif

const int GEN_POM_TRACE_LAYERS_MAX = 128;
const int GEN_POM_REFINE_STEPS_MAX = 8;
const int GEN_POM_SHADOW_TAPS_MAX = 64;
const int GEN_POM_AO_TAPS = 8;
// Tangent-space view-Z floor used for grazing rays. Do not use this as a hard cutoff:
// close, low-angle ground views can legitimately sit below this value and should still
// show relief; the steer/UV caps below keep those rays bounded.
const float GEN_POM_MIN_VIEW_Z = 0.012;
// Soft cap on |tan(theta)| = |V.xy|/V.z before parallax scale (preserves direction; avoids bent rays).
const float GEN_POM_STEER_TAN_CAP = 6.0;

uniform int   uParallaxTraceLayers;
uniform int   uParallaxRefineSteps;
uniform int   uParallaxShadowSamples;
uniform float uParallaxShadowSoftness;
uniform float uParallaxMaxUvShift;
uniform float uParallaxUvScale;
uniform vec2 uParallaxHeightTexSize;
#ifdef GENESIS_MATERIAL_TEXTURE_ARRAYS
uniform sampler2DArray uHeightArray;
uniform int uGenesisUseMaterialTextureArray;
#endif

vec2 pomTileLocal(vec2 uv)
{
    return fract(uv);
}

vec2 pomTileUv(vec2 tileBase, vec2 localUv)
{
    return tileBase + fract(localUv);
}

float sampleHeight01Grad(sampler2D heightTex, vec2 uv, vec2 dx, vec2 dy)
{
#ifdef GENESIS_MATERIAL_TEXTURE_ARRAYS
    if (uGenesisUseMaterialTextureArray > 0)
    {
        return 1.0 - textureGrad(uHeightArray, vec3(uv, float(genesisMaterialTextureLayer(0))), dx, dy).r;
    }
#endif
    return 1.0 - textureGrad(heightTex, uv, dx, dy).r;
}

// Matches UV displacement scale used for the view-ray march (also applied to parallax self-shadow).
float pomUvDisplacementScale(float strength)
{
    return clamp(strength, 0.0, 1.0) * 0.92;
}

// Trace POM from sampled height map. Returns the displaced UV; writes the surface depth at that hit
// (0 at the surface, 1 fully embedded) so the self-shadow trace can start from the correct layer.
//   uv0      : original surface UV
//   Vtan     : view direction in tangent space (normalized)
//   strength : height scale in 0..1 of layer thickness
vec2 traceParallaxPom(sampler2D heightTex, vec2 uv0, vec3 Vtan, float strength, vec2 dx, vec2 dy, out float hitDepth)
{
    hitDepth = 0.0;
    if (strength <= 0.0)
    {
        return uv0;
    }

    // Front-facing hemisphere only (view exits +N side). Using abs(z) or bending xy/z breaks coherent motion.
    if (Vtan.z <= 0.0)
    {
        return uv0;
    }

    int layers = clamp(uParallaxTraceLayers, 8, GEN_POM_TRACE_LAYERS_MAX);
    float layerStep = 1.0 / float(layers);

    // Classic parallax ray on the tangent plane: delta_uv proportional to V.xy/V.z (unnormalized steer ok up to cap).
    vec2 steer = Vtan.xy / max(Vtan.z, GEN_POM_MIN_VIEW_Z);
    float tanMag = length(steer);
    if (tanMag > GEN_POM_STEER_TAN_CAP)
    {
        steer *= GEN_POM_STEER_TAN_CAP / max(tanMag, GEN_EPS);
    }

    float parallaxScale = pomUvDisplacementScale(strength) * clamp(genesisParallaxUvScale(uParallaxUvScale), 0.02, 1.0);
    vec2 totalOffset = steer * parallaxScale;
    float maxUvShift = clamp(uParallaxMaxUvShift, 0.05, 0.75);
    float totalLen = length(totalOffset);
    if (totalLen > maxUvShift)
    {
        totalOffset *= maxUvShift / max(totalLen, GEN_EPS);
    }

    vec2 deltaUv = totalOffset * layerStep;

    vec2 tileBase = floor(uv0);
    vec2 curLocal = pomTileLocal(uv0);
    vec2 curUv = pomTileUv(tileBase, curLocal);
    float curLayer = 0.0;
    float curHeightSample = sampleHeight01Grad(heightTex, curUv, dx, dy);

    // Linear march until ray depth crosses sampled height.
    int marchSteps = 0;
    float prevHeightSample = curHeightSample;
    for (int i = 0; i < GEN_POM_TRACE_LAYERS_MAX; ++i)
    {
        if (i >= layers)
        {
            break;
        }

        if (curLayer >= curHeightSample)
        {
            break;
        }

        prevHeightSample = curHeightSample;
        curLocal -= deltaUv;
        curUv = pomTileUv(tileBase, curLocal);
        curLayer += layerStep;
        marchSteps++;
        curHeightSample = sampleHeight01Grad(heightTex, curUv, dx, dy);
    }

    if (marchSteps > 0)
    {
        vec2  prevLocal = curLocal + deltaUv;
        float prevLayer = curLayer - layerStep;
        float prevHeight = prevHeightSample;

        float afterDelta = curLayer - curHeightSample;
        float beforeDelta = prevLayer - prevHeight;
        float denom = max(afterDelta - beforeDelta, GEN_EPS);
        vec2 loLocal = prevLocal;
        float loLayer = prevLayer;
        vec2 hiLocal = curLocal;
        float hiLayer = curLayer;

        float secantT = clamp(-beforeDelta / denom, 0.0, 1.0);
        vec2 secantLocal = mix(prevLocal, curLocal, secantT);
        float secantLayer = mix(prevLayer, curLayer, secantT);
        float secantHeight = sampleHeight01Grad(heightTex, pomTileUv(tileBase, secantLocal), dx, dy);
        if (secantLayer >= secantHeight)
        {
            hiLocal = secantLocal;
            hiLayer = secantLayer;
        }
        else
        {
            loLocal = secantLocal;
            loLayer = secantLayer;
        }

        int refineSteps = clamp(uParallaxRefineSteps, 0, GEN_POM_REFINE_STEPS_MAX);
        for (int i = 0; i < GEN_POM_REFINE_STEPS_MAX; ++i)
        {
            if (i >= refineSteps)
            {
                break;
            }

            vec2  midLocal = 0.5 * (loLocal + hiLocal);
            float midLayer = 0.5 * (loLayer + hiLayer);
            float midH = sampleHeight01Grad(heightTex, pomTileUv(tileBase, midLocal), dx, dy);
            if (midLayer >= midH)
            {
                hiLocal = midLocal;
                hiLayer = midLayer;
            }
            else
            {
                loLocal = midLocal;
                loLayer = midLayer;
            }
        }

        curLocal = hiLocal;
        curLayer = hiLayer;
    }

    hitDepth = clamp(curLayer, 0.0, 1.0);
    return pomTileUv(tileBase, curLocal);
}

// Cheap parallax self-shadow: march toward the light from the hit point and accumulate the
// largest height-above-ray difference. Returns 1.0 (lit) .. 0.0 (fully shadowed); already gated
// to 1.0 if the light is below the surface in tangent space.
float traceParallaxShadow(sampler2D heightTex, vec2 uvHit, vec3 Ltan, float refDepth, float strength, vec2 dx, vec2 dy)
{
    if (Ltan.z <= 0.0 || strength <= 0.0)
    {
        return 1.0;
    }

    float lz = max(Ltan.z, GEN_EPS);
    int   taps = clamp(uParallaxShadowSamples, 4, GEN_POM_SHADOW_TAPS_MAX);
    float stepLen = refDepth / float(taps);
    if (stepLen <= 0.0)
    {
        return 1.0;
    }

    float uvScale = pomUvDisplacementScale(strength) * clamp(genesisParallaxUvScale(uParallaxUvScale), 0.02, 1.0);
    vec2 tileBase = floor(uvHit);
    vec2 localUv = pomTileLocal(uvHit);
    vec2 uvStep = (Ltan.xy / lz) * uvScale * stepLen;
    float curLayer = refDepth;
    float maxOcclusion = 0.0;
    float sumOcclusion = 0.0;
    float occlusionWeight = 0.0;
    float softWidth = max(stepLen * mix(0.35, 5.0, clamp(uParallaxShadowSoftness, 0.0, 4.0) * 0.25), 0.0015);

    for (int i = 0; i < GEN_POM_SHADOW_TAPS_MAX; ++i)
    {
        if (i >= taps || curLayer <= 0.0)
        {
            break;
        }

        localUv += uvStep;
        curLayer -= stepLen;
        float sampleH = sampleHeight01Grad(heightTex, pomTileUv(tileBase, localUv), dx, dy);
        if (curLayer < sampleH)
        {
            // Ray is above the surface here; no occluder.
            continue;
        }

        float delta = curLayer - sampleH;
        float occlusion = smoothstep(0.0, softWidth, delta);
        maxOcclusion = max(maxOcclusion, occlusion);
        sumOcclusion += occlusion;
        occlusionWeight += 1.0;
    }

    float avgOcclusion = occlusionWeight > 0.0 ? sumOcclusion / occlusionWeight : 0.0;
    float occlusion = mix(maxOcclusion, avgOcclusion, 0.28);
    return clamp(1.0 - occlusion, 0.0, 1.0);
}

// Contact AO from local height neighborhood around the POM hit point.
// This targets the "grounded" crevice darkening many packs pair with POM.
float traceParallaxAo(sampler2D heightTex, vec2 uvHit, float refDepth, float strength, float aoStrength, vec2 dx, vec2 dy)
{
    if (refDepth <= 0.0 || strength <= 0.0 || aoStrength <= 0.0)
    {
        return 1.0;
    }

    vec2 tileBase = floor(uvHit);
    vec2 localHit = pomTileLocal(uvHit);
    vec2 texelSize = vec2(1.0) / max(genesisParallaxHeightTexSize(uParallaxHeightTexSize), vec2(GEN_EPS));
    float radiusTexels = mix(0.75, 2.25, clamp(refDepth, 0.0, 1.0)) * clamp(strength, 0.0, 1.0);
    if (radiusTexels <= GEN_EPS)
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
        vec2 uv = pomTileUv(tileBase, localHit + tapDirs[i] * texelSize * radiusTexels * ring);
        float sampleH = sampleHeight01Grad(heightTex, uv, dx, dy);
        occ += max(0.0, sampleH - refDepth);
    }

    occ /= float(GEN_POM_AO_TAPS);
    // AO should ground local crevices, not replace normal/specular lighting response.
    return clamp(1.0 - occ * 1.35 * aoStrength, 0.78, 1.0);
}

#endif // GENESIS_PARALLAX_GLSL
