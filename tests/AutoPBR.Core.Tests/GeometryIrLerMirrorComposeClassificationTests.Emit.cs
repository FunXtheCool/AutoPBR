using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed partial class GeometryIrLerMirrorComposeClassificationTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", "net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", "net.minecraft.client.model.animal.polarbear.PolarBearModel")]
    public void Adult_panda_polar_catalog_static_mesh_legs_below_head(string texturePath, string expectedJvm)
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, 0f, 0f, out var mesh, out var provenance));
        Assert.Contains(expectedJvm, provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.Equal(
            CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot,
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
                expectedJvm,
                Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant(),
                texturePath));

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{expectedJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(expectedJvm, shard.RootElement);
        AssertLegsBelowHead(mesh!, geometryRoot, 64, 64, expectedJvm);
    }

    [Theory]
    [InlineData(CowJvm, 64, 64)]
    [InlineData(ColdCowJvm, 64, 64)]
    [InlineData(WarmCowJvm, 64, 64)]
    [InlineData(PandaJvm, 64, 64)]
    [InlineData(PolarBearJvm, 128, 64)]
    [InlineData(BabyPandaJvm, 64, 64)]
    [InlineData(BabyPolarBearJvm, 128, 64)]
    [InlineData(AdultCatJvm, 64, 64)]
    public void Flat_quadruped_ir_emit_body_centroid_between_legs_and_head_when_shard_ok(
        string officialJvm,
        int atlasW,
        int atlasH)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, shard.RootElement);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            profile,
            officialJvm,
            atlasW,
            atlasH,
            out var failure,
            geometryRootOverride: geometryRoot);
        Assert.NotNull(mesh);
        Assert.Null(failure);
        AssertQuadrupedBodyBetweenLegsAndHead(mesh!, geometryRoot, atlasW, atlasH, officialJvm);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cat/cat_all_black.png", AdultCatJvm)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", CowJvm)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold.png", ColdCowJvm)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_warm.png", WarmCowJvm)]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", PandaJvm)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", PolarBearJvm)]
    public void Catalog_static_mesh_body_centroid_between_legs_and_head(
        string texturePath,
        string expectedJvm)
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            profile,
            0f,
            0f,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Contains(expectedJvm, provenance.Detail ?? "", StringComparison.Ordinal);

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{expectedJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(expectedJvm, shard.RootElement);
        var (atlasW, atlasH) = expectedJvm.Contains("polarbear", StringComparison.OrdinalIgnoreCase) ? (128, 64) : (64, 64);
        AssertQuadrupedBodyBetweenLegsAndHead(mesh!, geometryRoot, atlasW, atlasH, expectedJvm);
    }

    [Fact]
    public void Cow_parity_emit_legs_below_head_in_ler_preview_space_when_shard_ok()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!GeometryIrTestTierSupport.IsClientJarPresent(repo))
        {
            return;
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CowJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(CowJvm, shard.RootElement);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/cow/cow_temperate",
            profile,
            CowJvm,
            64,
            64,
            out var failure,
            geometryRootOverride: geometryRoot);
        Assert.NotNull(mesh);
        Assert.Null(failure);
        AssertLegsBelowHead(mesh!, geometryRoot, 64, 64, CowJvm);
    }

    [Fact]
    public void Pig_parity_emit_legs_below_head_in_ler_preview_space_when_shard_ok()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!GeometryIrTestTierSupport.IsClientJarPresent(repo))
        {
            return;
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/pig/pig",
            profile,
            PigJvm,
            64,
            64,
            out var failure,
            geometryRootOverride: geometryRoot);
        Assert.NotNull(mesh);
        Assert.Null(failure);
        AssertLegsBelowHead(mesh!, geometryRoot, 64, 64, PigJvm);
    }
}
