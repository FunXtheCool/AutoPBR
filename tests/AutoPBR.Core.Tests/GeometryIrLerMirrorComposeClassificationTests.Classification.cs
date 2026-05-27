using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

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
        Assert.True(CleanRoomEntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
        Assert.False(CleanRoomEntityModelRuntime.UsesFlatPartPoseOffsetQuadrupedJvm(officialJvmName));
        Assert.False(
            CleanRoomEntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain(
                stem,
                normalizedAssetPath: ""));
        Assert.False(
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
    }

    [Theory]
    [InlineData(AbstractFelineJvm, "abstractfelinemodel")]
    [InlineData(AdultFelineJvm, "adultfelinemodel")]
    public void Feline_host_mesh_resolves_cow_class_ler_via_jvm_not_stem_cat_substring(
        string officialJvmName,
        string stem)
    {
        Assert.False(CleanRoomEntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
        Assert.True(CleanRoomEntityModelRuntime.UsesFlatPartPoseOffsetQuadrupedJvm(officialJvmName));
        Assert.True(
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
        Assert.False(
            CleanRoomEntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain(
                stem,
                normalizedAssetPath: ""));
    }

    [Fact]
    public void AdultFeline_default_ler_inverts_head_leg_centroids()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultFelineJvm}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(AdultFelineJvm, doc.RootElement.Clone());

        var defaultMesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test", AdultFelineJvm, 64, 64, repaired, lerMirrorRightComposeLocalChain: false, out var err0);
        var rightMesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test", AdultFelineJvm, 64, 64, repaired, lerMirrorRightComposeLocalChain: true, out var err1);
        Assert.NotNull(defaultMesh);
        Assert.NotNull(rightMesh);
        Assert.Null(err0);
        Assert.Null(err1);

        var (defaultHead, defaultLeg) = MeasureHeadLegCentroidYPair(defaultMesh!, repaired, 64, 64, AdultFelineJvm);
        var (rightHead, rightLeg) = MeasureHeadLegCentroidYPair(rightMesh!, repaired, 64, 64, AdultFelineJvm);
        // Nested feline host: default S*L keeps +Y corner centroids; cow-class L*S folds to -Y LER preview space.
        Assert.True(defaultLeg > 0f && rightLeg < 0f,
            $"default +Y vs right-compose -Y: default leg={defaultLeg:F3} head={defaultHead:F3}; right leg={rightLeg:F3} head={rightHead:F3}");
        Assert.True(rightLeg < rightHead, $"right-compose LER: legY={rightLeg:F3} headY={rightHead:F3}");
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
    public void Flat_offset_quadrupeds_resolve_right_compose_local_chain(string officialJvmName, string stem)
    {
        Assert.True(
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath: null));
    }

    [Theory]
    [InlineData(PandaJvm, "pandamodel", "assets/minecraft/textures/entity/panda/panda.png")]
    [InlineData(PolarBearJvm, "polarbearmodel", "assets/minecraft/textures/entity/bear/polarbear.png")]
    public void Panda_polar_bear_adult_resolve_cow_class_right_compose_ler(
        string officialJvmName,
        string stem,
        string normalizedAssetPath)
    {
        Assert.True(
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvmName,
                stem,
                normalizedAssetPath));
        Assert.False(
            CleanRoomEntityModelRuntime.UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName));
    }

    [Fact]
    public void Equine_jvm_uses_dedicated_geometry_ir_preview_basis_not_stem_quadruped_gate()
    {
        Assert.True(
            CleanRoomEntityModelRuntime.UsesEquineGeometryIrPreviewBasis(
                HorseJvm,
                "horsemodel",
                normalizedAssetPath: null));
        Assert.False(
            CleanRoomEntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain(
                "horsemodel",
                normalizedAssetPath: ""));
        Assert.Equal(
            CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.EquineDedicated,
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
                HorseJvm,
                "horsemodel",
                "assets/minecraft/textures/entity/horse/horse_white.png"));
    }

    [Theory]
    [InlineData(CowJvm, "cowmodel", "", "RightComposeLocalChain")]
    [InlineData(AdultFelineJvm, "adultfelinemodel", "", "RightComposeLocalChain")]
    [InlineData(RabbitJvm, "rabbitmodel", "", "StandardWorldRoot")]
    [InlineData("net.minecraft.client.model.monster.hoglin.HoglinModel", "hoglinmodel", "", "StandardWorldRoot")]
    [InlineData("", "arrow", "assets/minecraft/textures/entity/projectiles/arrow.png", "Skip")]
    public void Shared_ler_basis_resolver_classifies_viewport_policy(
        string officialJvm,
        string stem,
        string normalizedAssetPath,
        string expected)
    {
        Assert.Equal(
            expected,
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(officialJvm, stem, normalizedAssetPath).ToString());
    }
}
