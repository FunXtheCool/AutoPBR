// Eight unrolled froxel march samples in one function (no nested helpers, no loops).
// Requires uniform sampler2DArray uFroxelVolume declared before this include.

#ifndef GENESIS_VOLUME_INTEGRATE_MARCH8_TEXTURE_GLSL
#define GENESIS_VOLUME_INTEGRATE_MARCH8_TEXTURE_GLSL

vec4 viMarch8Texture(vec4 state, float jitter, float stepLen, vec3 rd, vec3 cameraPos, vec3 camRight, vec3 camUp,
    vec3 camForward, vec3 halfExtent, int sliceCount, float depthDistribution, float miePhase, float extinction)
{
    vec3 accum = state.rgb;
    float transmittance = state.a;

    if (transmittance >= 0.02)
    {
        float t = jitter + 0.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 1.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 2.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 3.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 4.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 5.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 6.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }
    if (transmittance >= 0.02)
    {
        float t = jitter + 7.5 * stepLen;
        vec3 worldPos = cameraPos + rd * t;
        vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent, sliceCount,
            depthDistribution);
        if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0)
        {
            vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
            float sliceCoord = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
            vec4 voxel = texture(uFroxelVolume, vec3(vfUv, sliceCoord));
            float density = voxel.r;
            if (density > 1e-5)
            {
                vec3 sunScatter = vec3(voxel.g, voxel.b, voxel.a) * miePhase;
                float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
                accum += transmittance * sunScatter * inscatterW * 3.8;
                transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
            }
        }
    }

    return vec4(accum, transmittance);
}

#endif // GENESIS_VOLUME_INTEGRATE_MARCH8_TEXTURE_GLSL
