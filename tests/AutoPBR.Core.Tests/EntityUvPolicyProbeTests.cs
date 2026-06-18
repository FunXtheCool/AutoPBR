using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class EntityUvPolicyProbeTests
{
    private readonly ITestOutputHelper _output;

    public EntityUvPolicyProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Probe_creeper_policy_combo_for_legacy_fingerprint()
    {
        UvDebugSettings.ResetAllOverrides();
        const ulong legacy = 0x5d24fe3716be89a5UL;
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/creeper/creeper.png", profile, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        foreach (var mapJava in new[] { false, true })
        foreach (var flipV in new[] { false, true })
        foreach (var useBl in new[] { false, true })
        foreach (var swap in new[] { false, true })
        {
            var policy = new PreviewUvBakePolicy
            {
                MapJavaCuboidFaceCorners = mapJava,
                FlipV = flipV,
                UseBottomLeftUvOrigin = useBl,
                SwapFaceUpDown = swap,
                PreserveDirectionalBounds = true,
            };
            Assert.True(MinecraftModelBaker.TryBakeWithUvPolicy(
                merged, "minecraft", pathToIdx, texSizes, in policy, out var verts, out _, out _));
            var fp = Fingerprint(verts);
            var tag = fp == legacy ? "MATCH" : "     ";
            _output.WriteLine($"{tag} mapJava={mapJava} flipV={flipV} useBL={useBl} swap={swap} fp=0x{fp:x16}");
        }

        var baseline = PreviewUvBakePolicy.EntityCuboidBaseline;
        Assert.True(MinecraftModelBaker.TryBakeWithUvPolicy(
            merged, "minecraft", pathToIdx, texSizes, in baseline, out var baselineVerts, out _, out _));
        _output.WriteLine($"      entityBaseline fp=0x{Fingerprint(baselineVerts):x16}");
    }

    private static ulong Fingerprint(float[] verts)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            const int stride = 12;
            for (var i = 6; i < verts.Length; i += stride)
            {
                hash ^= BitConverter.SingleToUInt32Bits(verts[i]);
                hash *= 1099511628211UL;
                hash ^= BitConverter.SingleToUInt32Bits(verts[i + 1]);
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
