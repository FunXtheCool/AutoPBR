using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>Rotated horn child parts under head must match Java reference preview origins.</summary>
public sealed class ColdCowHornReferencePoseTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Cold_cow_horn_part_origins_match_reference_java_ler_preview_space()
    {
        const string jvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, ir.RootElement);
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            Profile26,
            jvm,
            64,
            64,
            out var err,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var emitOptions = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm };
        var cmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh,
            emitOptions,
            tolerance: 0.15);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    private static (JsonDocument? Reference, JsonDocument? Ir) LoadPair(string jvm)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(refPath) || !GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return (null, null);
        }

        return (JsonDocument.Parse(File.ReadAllText(refPath)), JsonDocument.Parse(File.ReadAllText(shardPath)));
    }
}
