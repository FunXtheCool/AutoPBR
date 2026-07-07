using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Focused reference_java vs IR vs parity-emit world-space probes for flat quadruped body placement.
/// Surfaces lift/walk vs Java <c>worldPose</c> vs LER preview assembly without conflating animation paths.
/// </summary>
public sealed class QuadrupedReferenceWorldPoseAnalysisTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private readonly ITestOutputHelper _output;

    public QuadrupedReferenceWorldPoseAnalysisTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> FlatQuadrupedJvmCases() =>
    [
        ["net.minecraft.client.model.animal.cow.CowModel", 64, 64],
        ["net.minecraft.client.model.animal.cow.ColdCowModel", 64, 64],
        ["net.minecraft.client.model.animal.panda.PandaModel", 64, 64],
        ["net.minecraft.client.model.animal.polarbear.PolarBearModel", 128, 64],
        ["net.minecraft.client.model.monster.creeper.CreeperModel", 64, 32],
        ["net.minecraft.client.model.animal.pig.PigModel", 64, 64],
        ["net.minecraft.client.model.animal.sheep.SheepModel", 64, 64],
    ];

    [Theory]
    [MemberData(nameof(FlatQuadrupedJvmCases))]
    public void Report_reference_java_world_pose_vs_ir_walk_vs_parity_emit(string jvm, int atlasW, int atlasH)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(refPath) || !File.Exists(shardPath))
        {
            return;
        }

        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);

        Assert.True(
            GeometryIrMeshWalk.TryCollectBakedWorldTranslations(
                reference.RootElement, out var refBaked, out var refBakedFail),
            refBakedFail);
        Assert.True(
            GeometryIrMeshWalk.TryCollectPartWorldTranslations(
                repaired, Matrix4x4.Identity, out var irWalk, out var irWalkFail),
            irWalkFail);

        var repairedVsRef = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            reference.RootElement, repaired, tolerance: 0.05);
        var synced = GeometryIrReferencePoseSync.ApplyForComparisons(reference.RootElement, repaired);
        var syncedVsRef = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            reference.RootElement, synced, tolerance: 0.05);

        var meshDefault = EntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test", jvm, atlasW, atlasH, repaired, lerMirrorRightComposeLocalChain: false, out _);
        var meshRight = EntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test", jvm, atlasW, atlasH, repaired, lerMirrorRightComposeLocalChain: true, out _);
        var meshPolicy = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", Profile26, jvm, atlasW, atlasH, out _, geometryRootOverride: repaired);

        Assert.NotNull(meshDefault);
        Assert.NotNull(meshRight);
        Assert.NotNull(meshPolicy);

        var policyBasis = EntityModelRuntime.ResolveGeometryIrLerBasis(jvm, "", null);

        _output.WriteLine($"=== {jvm} ===");
        _output.WriteLine($"IR walk vs reference baked worldPose: match={repairedVsRef.IsMatch} msg={repairedVsRef.Message}");
        _output.WriteLine($"IR pose-sync vs reference: match={syncedVsRef.IsMatch} msg={syncedVsRef.Message}");
        _output.WriteLine($"Policy LER basis: {policyBasis}");

        foreach (var partId in new[] { "body", "head", "right_hind_leg", "left_front_leg" })
        {
            refBaked.TryGetValue(partId, out var refO);
            irWalk.TryGetValue(partId, out var walkO);
            var bodyCyDefault = TryMeanPartCenterY(meshDefault!, repaired, atlasW, atlasH, jvm, partId);
            var bodyCyRight = TryMeanPartCenterY(meshRight!, repaired, atlasW, atlasH, jvm, partId);
            var bodyCyPolicy = TryMeanPartCenterY(meshPolicy!, repaired, atlasW, atlasH, jvm, partId);

            _output.WriteLine(
                $"{partId}\trefWorld=({refO.X:0.###},{refO.Y:0.###},{refO.Z:0.###})\t" +
                $"irWalk=({walkO.X:0.###},{walkO.Y:0.###},{walkO.Z:0.###})\t" +
                $"meshDefaultY={bodyCyDefault:0.###}\tmeshRightY={bodyCyRight:0.###}\tmeshPolicyY={bodyCyPolicy:0.###}");
        }

        // Informational: documents whether per-JVM lift needs rework vs preview LER policy.
        if (refBaked.TryGetValue("body", out var refBody) && irWalk.TryGetValue("body", out var walkBody))
        {
            var walkDelta = Vector3.Distance(refBody, walkBody);
            _output.WriteLine($"body origin |ref - irWalk| = {walkDelta:0.###} (large => walk/compose gap, not missing shard)");
        }
    }

    private static float TryMeanPartCenterY(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string jvm,
        string partId)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = jvm,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        if (partIds.Count != mesh.Elements.Count)
        {
            return float.NaN;
        }

        var sum = 0f;
        var count = 0;
        for (var i = 0; i < partIds.Count; i++)
        {
            if (!string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                continue;
            }

            sum += MeasureElementCenterY(mesh.Elements[i]);
            count++;
        }

        return count == 0 ? float.NaN : sum / count;
    }

    private static float MeasureElementCenterY(ModelElement element)
    {
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (element.From[0], element.From[1], element.From[2]),
            (element.To[0], element.From[1], element.From[2]),
            (element.From[0], element.To[1], element.From[2]),
            (element.To[0], element.To[1], element.From[2]),
            (element.From[0], element.From[1], element.To[2]),
            (element.To[0], element.From[1], element.To[2]),
            (element.From[0], element.To[1], element.To[2]),
            (element.To[0], element.To[1], element.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), element.LocalToParent);
            minY = MathF.Min(minY, w.Y);
            maxY = MathF.Max(maxY, w.Y);
        }

        return (minY + maxY) * 0.5f;
    }
}
