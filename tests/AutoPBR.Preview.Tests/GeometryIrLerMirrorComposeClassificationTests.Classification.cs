using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed partial class GeometryIrLerMirrorComposeClassificationTests
{
    [Theory]
    [InlineData(BabyRabbitJvm, "babyrabbitmodel")]
    [InlineData(RabbitJvm, "rabbitmodel")]
    public void Rabbit_host_mesh_resolves_default_ler_not_stem_quadruped_right_compose(
        string officialJvmName,
        string stem)
    {
        Assert.True(EntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
        Assert.False(EntityModelRuntime.UsesFlatPartPoseOffsetQuadrupedJvm(officialJvmName));
        Assert.False(
            EntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain(
                stem,
                normalizedAssetPath: ""));
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
    }

    [Theory]
    [InlineData(AbstractFelineJvm, "abstractfelinemodel")]
    [InlineData(AdultFelineJvm, "adultfelinemodel")]
    public void Feline_host_mesh_resolves_column_pose_stack_root_ler_via_jvm_not_stem_cat_substring(
        string officialJvmName,
        string stem)
    {
        Assert.False(EntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
        Assert.True(EntityModelRuntime.UsesFlatPartPoseOffsetQuadrupedJvm(officialJvmName));
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
        Assert.Equal(
            EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot,
            EntityModelRuntime.ResolveGeometryIrLerBasis(officialJvmName, stem, normalizedAssetPath: null));
    }

    [Fact]
    public void AdultFeline_column_root_ler_orders_legs_below_head()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultFelineJvm}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(AdultFelineJvm, doc.RootElement.Clone());

        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            AdultFelineJvm,
            64,
            64,
            out var err,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var (headY, legY) = MeasureHeadLegCentroidYPair(mesh!, repaired, 64, 64, AdultFelineJvm);
        Assert.True(legY < headY, $"column-root feline: legY={legY:F3} headY={headY:F3}");
    }

    [Theory]
    [InlineData(AbstractFelineJvm, 64, 64)]
    [InlineData(AdultFelineJvm, 64, 64)]
    public void Feline_parity_emit_legs_below_head_in_ler_preview_space_when_shard_ok(
        string officialJvm,
        int atlasW,
        int atlasH)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, shard.RootElement);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            profile,
            officialJvm,
            atlasW,
            atlasH,
            out var failure,
            geometryRootOverride: geometryRoot);
        Assert.NotNull(mesh);
        Assert.Null(failure);
        AssertLegsBelowHead(mesh!, geometryRoot, atlasW, atlasH, officialJvm);
    }

    [Theory]
    [InlineData(CowJvm, "cowmodel")]
    [InlineData(ColdCowJvm, "cow_cold")]
    [InlineData(WarmCowJvm, "cow_warm")]
    [InlineData(PigJvm, "pigmodel")]
    [InlineData(WolfJvm, "wolfmodel")]
    [InlineData(CreeperJvm, "creepermodel")]
    [InlineData(TurtleJvm, "turtlemodel")]
    [InlineData(QuadrupedJvm, "quadrupedmodel")]
    public void Flat_offset_quadrupeds_resolve_column_pose_stack_root_ler(string officialJvmName, string stem)
    {
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
        Assert.Equal(
            EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot,
            EntityModelRuntime.ResolveGeometryIrLerBasis(officialJvmName, stem, normalizedAssetPath: null));
    }

    [Theory]
    [InlineData(PandaJvm, "pandamodel", "assets/minecraft/textures/entity/panda/panda.png")]
    [InlineData(PolarBearJvm, "polarbearmodel", "assets/minecraft/textures/entity/bear/polarbear.png")]
    public void Panda_polar_bear_adult_resolve_column_pose_stack_root_ler(
        string officialJvmName,
        string stem,
        string normalizedAssetPath)
    {
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath));
        Assert.Equal(
            EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot,
            EntityModelRuntime.ResolveGeometryIrLerBasis(officialJvmName, stem, normalizedAssetPath));
        Assert.False(
            EntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
    }

    [Fact]
    public void Equine_jvm_uses_standard_column_root_ler_not_legacy_right_compose()
    {
        const string path = "assets/minecraft/textures/entity/horse/horse_white.png";
        Assert.True(
            EntityModelRuntime.UsesEquineGeometryIrPreviewBasis(
                HorseJvm,
                "horsemodel",
                path));
        Assert.False(
            EntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain(
                "horsemodel",
                path));
        Assert.Equal(
            EntityModelRuntime.GeometryIrLerBasisKind.EquineDedicated,
            EntityModelRuntime.ResolveGeometryIrLerBasis(HorseJvm, "horsemodel", path));
    }

    [Theory]
    [InlineData(CowJvm, "cowmodel", "", "StandardWorldRoot")]
    [InlineData(AdultFelineJvm, "adultfelinemodel", "", "StandardWorldRoot")]
    [InlineData(RabbitJvm, "rabbitmodel", "", "StandardWorldRoot")]
    [InlineData("net.minecraft.client.model.monster.hoglin.HoglinModel", "hoglinmodel", "", "StandardWorldRoot")]
    [InlineData("", "arrow", "assets/minecraft/textures/entity/projectiles/arrow.png", "Skip")]
    [InlineData(
        "net.minecraft.client.model.object.armorstand.ArmorStandModel",
        "armorstand",
        "assets/minecraft/textures/entity/armorstand/armorstand.png",
        "StandardWorldRoot")]
    [InlineData("net.minecraft.client.model.monster.ghast.GhastModel", "ghast", "", "StandardWorldRoot")]
    [InlineData("net.minecraft.client.model.GhastModel", "ghast", "", "StandardWorldRoot")]
    [InlineData("", "ghast", "assets/minecraft/textures/entity/ghast/ghast.png", "StandardWorldRoot")]
    [InlineData("", "happy_ghast", "assets/minecraft/textures/entity/ghast/happy_ghast.png", "StandardWorldRoot")]
    public void Shared_ler_basis_resolver_classifies_viewport_policy(
        string officialJvm,
        string stem,
        string normalizedAssetPath,
        string expected)
    {
        Assert.Equal(
            expected,
            EntityModelRuntime.ResolveGeometryIrLerBasis(officialJvm, stem, normalizedAssetPath).ToString());
    }
}
