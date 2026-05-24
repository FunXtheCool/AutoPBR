using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Locks LER <c>scale(-1,-1,1)</c> multiply order for flat <c>PartPose.offset</c> quadruped pilots
/// (<c>LocalToParent * S</c> vs default <c>S * LocalToParent</c>).
/// </summary>
public sealed class GeometryIrLerMirrorComposeClassificationTests
{
    private const string CowJvm = "net.minecraft.client.model.animal.cow.CowModel";
    private const string ColdCowJvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
    private const string WarmCowJvm = "net.minecraft.client.model.animal.cow.WarmCowModel";
    private const string PandaJvm = "net.minecraft.client.model.animal.panda.PandaModel";
    private const string PolarBearJvm = "net.minecraft.client.model.animal.polarbear.PolarBearModel";
    private const string BabyPandaJvm = "net.minecraft.client.model.animal.panda.BabyPandaModel";
    private const string BabyPolarBearJvm = "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel";
    private const string PigJvm = "net.minecraft.client.model.animal.pig.PigModel";
    private const string WolfJvm = "net.minecraft.client.model.animal.wolf.WolfModel";
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";
    private const string TurtleJvm = "net.minecraft.client.model.animal.turtle.TurtleModel";
    private const string QuadrupedJvm = "net.minecraft.client.model.QuadrupedModel";
    private const string HorseJvm = "net.minecraft.client.model.animal.equine.HorseModel";
    private const string AbstractFelineJvm = "net.minecraft.client.model.animal.feline.AbstractFelineModel";
    private const string AdultFelineJvm = "net.minecraft.client.model.animal.feline.AdultFelineModel";
    private const string BabyRabbitJvm = "net.minecraft.client.model.animal.rabbit.BabyRabbitModel";
    private const string RabbitJvm = "net.minecraft.client.model.animal.rabbit.RabbitModel";

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
        Assert.False(defaultLeg < defaultHead, $"default LER: legY={defaultLeg:F3} headY={defaultHead:F3}");
        Assert.True(rightLeg < rightHead, $"right-compose LER: legY={rightLeg:F3} headY={rightHead:F3}");
    }

    private static (float HeadY, float LegY) MeasureHeadLegCentroidYPair(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && legCount > 0);
        return (headSum / headCount, legSum / legCount);
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
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", "net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", "net.minecraft.client.model.animal.polarbear.PolarBearModel")]
    public void Adult_panda_polar_catalog_static_mesh_legs_below_head(string texturePath, string expectedJvm)
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, 0f, 0f, out var mesh, out var provenance));
        Assert.Contains(expectedJvm, provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.True(
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
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
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, 0f, 0f, out var mesh, out var provenance));
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

    private static void AssertQuadrupedBodyBetweenLegsAndHead(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var (headY, bodyY, legY) = MeasureHeadBodyLegCentroidY(mesh, geometryRoot, atlasW, atlasH, officialJvmName);
        Assert.True(legY < headY, $"{officialJvmName}: legY={legY:F3} headY={headY:F3}");
        var minY = MathF.Min(legY, MathF.Min(bodyY, headY));
        var maxY = MathF.Max(legY, MathF.Max(bodyY, headY));
        Assert.True(
            bodyY >= minY - 0.5f && bodyY <= maxY + 0.5f,
            $"{officialJvmName}: bodyY={bodyY:F3} outside [{minY:F3},{maxY:F3}]");
        var spanLimit = officialJvmName.Contains("polarbear", StringComparison.OrdinalIgnoreCase) ? 26f : 22f;
        Assert.True(maxY - minY < spanLimit, $"{officialJvmName}: vertical span={maxY - minY:F3}");
    }

    private static (float HeadY, float BodyY, float LegY) MeasureHeadBodyLegCentroidY(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float bodySum = 0f;
        var bodyCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase) &&
                     !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += cy;
                bodyCount++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && bodyCount > 0 && legCount > 0);
        return (headSum / headCount, bodySum / bodyCount, legSum / legCount);
    }

    private static void AssertLegsBelowHead(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && legCount > 0);
        Assert.True(legSum / legCount < headSum / headCount);
    }

    private static void TransformWorldCorners(
        ModelElement el,
        out Vector3 wMin,
        out Vector3 wMax)
    {
        wMin = new Vector3(float.PositiveInfinity);
        wMax = new Vector3(float.NegativeInfinity);
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (el.From[0], el.From[1], el.From[2]),
            (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]),
            (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]),
            (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]),
            (el.To[0], el.To[1], el.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }
    }
}
