// Froxel march helpers: one step + fixed unroll inside a single function (ANGLE rejects texelFetch in
// for-loops and miscompiles many march calls from main()). Requires uniform uFroxelVolume before include.

#ifndef GENESIS_VOLUME_INTEGRATE_MARCH_GLSL
#define GENESIS_VOLUME_INTEGRATE_MARCH_GLSL

vec4 viMarchOne(vec4 accumTrans, float t, vec3 rd, vec3 cameraPos, vec3 camRight, vec3 camUp, vec3 camForward,
    vec3 halfExtent, int sliceCount, float depthDistribution, float miePhase, float stepLen, float extinction,
    ivec3 froxelVolSize)
{
    vec3 accum = accumTrans.rgb;
    float transmittance = accumTrans.a;
    vec3 worldPos = cameraPos + rd * t;
    vec3 froxelUv = vfWorldToFroxelUv(worldPos, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution);
    if (froxelUv.x >= 0.01 && froxelUv.x <= 0.99 && froxelUv.y >= 0.01 && froxelUv.y <= 0.99 && froxelUv.z >= 0.0
        && transmittance >= 0.02)
    {
        float vfZ = clamp(froxelUv.z, 0.0, float(sliceCount) - 1.001);
        int vfZ0 = int(floor(vfZ));
        int vfZ1 = min(vfZ0 + 1, sliceCount - 1);
        float vfFz = vfZ - float(vfZ0);
        vec2 vfUv = clamp(froxelUv.xy, vec2(0.001), vec2(0.999));
        ivec2 vfPix = ivec2(vfUv * vec2(froxelVolSize.xy) - vec2(0.5));
        vfPix = clamp(vfPix, ivec2(0), froxelVolSize.xy - ivec2(1));
        vec4 vfS0 = texelFetch(uFroxelVolume, ivec3(vfPix, vfZ0), 0);
        vec4 vfS1 = texelFetch(uFroxelVolume, ivec3(vfPix, vfZ1), 0);
        vec4 voxel = mix(vfS0, vfS1, vfFz);
        float density = voxel.r;
        if (density > 1e-5)
        {
            vec3 sunScatter;
            sunScatter.r = voxel.g * miePhase;
            sunScatter.g = voxel.b * miePhase;
            sunScatter.b = voxel.a * miePhase;
            float inscatterW = vmSegmentInscatterWeight(density, stepLen, extinction);
            accum += transmittance * sunScatter * inscatterW * 3.8;
            transmittance *= vmSegmentTransmittance(density, stepLen, extinction);
        }
    }

    return vec4(accum, transmittance);
}

vec4 viIntegrateMarch8(vec4 state, float jitter, float stepLen, vec3 rd, vec3 cameraPos, vec3 camRight, vec3 camUp,
    vec3 camForward, vec3 halfExtent, int sliceCount, float depthDistribution, float miePhase, float extinction,
    ivec3 froxelVolSize)
{
    state = viMarchOne(state, jitter + (0.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (1.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (2.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (3.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (4.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (5.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (6.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    state = viMarchOne(state, jitter + (7.5) * stepLen, rd, cameraPos, camRight, camUp, camForward, halfExtent,
        sliceCount, depthDistribution, miePhase, stepLen, extinction, froxelVolSize);
    return state;
}

#endif // GENESIS_VOLUME_INTEGRATE_MARCH_GLSL
