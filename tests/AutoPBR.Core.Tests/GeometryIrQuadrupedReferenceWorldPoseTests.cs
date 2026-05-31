using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Static bind pose: parity mesh world origins vs Java <c>reference_java</c> + LER <c>scale(-1,-1,1)</c>.
/// </summary>
public sealed class GeometryIrQuadrupedReferenceWorldPoseTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Fact]
    public void Living_entity_renderer_root_scale_negates_part_origin_y()
    {
        var s = CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale;
        var t = Matrix4x4.CreateTranslation(0f, 4f, -8f);
        var modelOrigin = Vector3.Transform(Vector3.Zero, t);
        Assert.Equal(4f, modelOrigin.Y, precision: 3);

        var st = Matrix4x4.Multiply(s, t);
        var ts = Matrix4x4.Multiply(t, s);
        var previewViaRefPoint = Vector3.Transform(modelOrigin, s);
        var previewViaSt = Vector3.Transform(Vector3.Zero, st);
        var previewViaTs = Vector3.Transform(Vector3.Zero, ts);
        Assert.Equal(-4f, previewViaRefPoint.Y, precision: 3);
        Assert.Equal(-4f, previewViaTs.Y, precision: 3);
        Assert.NotEqual(-4f, previewViaSt.Y, precision: 3);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel", 64, 64)]
    [InlineData("net.minecraft.client.model.animal.cow.ColdCowModel", 64, 64)]
    [InlineData("net.minecraft.client.model.animal.panda.PandaModel", 64, 64)]
    [InlineData("net.minecraft.client.model.animal.polarbear.PolarBearModel", 128, 64)]
    [InlineData("net.minecraft.client.model.monster.creeper.CreeperModel", 64, 32)]
    [InlineData("net.minecraft.client.model.animal.pig.PigModel", 64, 64)]
    public void Parity_mesh_part_origins_match_reference_java_ler_preview_space(
        string jvm,
        int atlasW,
        int atlasH)
    {
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
            atlasW,
            atlasH,
            out var err,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var emitOptions = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm };
        var lerCmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh,
            emitOptions,
            tolerance: 0.35);
        Assert.True(lerCmp.IsMatch, lerCmp.Message);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel", 64, 64)]
    public void Parity_mesh_model_space_matches_reference_java_before_ler_basis(
        string jvm,
        int atlasW,
        int atlasH)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, ir.RootElement);
        var emitOptions = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm };
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "entity/test",
            jvm,
            atlasW,
            atlasH,
            repaired,
            out var err);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var modelCmp = GeometryIrReferenceComparer.CompareReferenceJavaModelWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh,
            emitOptions,
            tolerance: 0.35);
        Assert.True(modelCmp.IsMatch, modelCmp.Message);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", "net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", "net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("assets/minecraft/textures/entity/creeper/creeper.png", "net.minecraft.client.model.monster.creeper.CreeperModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", "net.minecraft.client.model.animal.cow.BabyCowModel")]
    [InlineData("assets/minecraft/textures/entity/fox/fox_baby.png", "net.minecraft.client.model.animal.fox.BabyFoxModel")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png", "net.minecraft.client.model.animal.chicken.BabyChickenModel")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_british_shorthair_baby.png", "net.minecraft.client.model.animal.feline.BabyCatModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel")]
    [InlineData("assets/minecraft/textures/entity/horse/horse_black_baby.png", "net.minecraft.client.model.animal.equine.BabyHorseModel")]
    [InlineData("assets/minecraft/textures/entity/horse/donkey_baby.png", "net.minecraft.client.model.animal.equine.BabyDonkeyModel")]
    [InlineData("assets/minecraft/textures/entity/goat/goat_baby.png", "net.minecraft.client.model.animal.goat.BabyGoatModel")]
    [InlineData("assets/minecraft/textures/entity/pig/pig_temperate_baby.png", "net.minecraft.client.model.animal.pig.BabyPigModel")]
    public void Catalog_static_mesh_part_origins_match_reference_java_ler_preview_space(
        string texturePath,
        string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        var isBaby = stem.Contains("baby", StringComparison.Ordinal) ||
            texturePath.Contains("_baby", StringComparison.OrdinalIgnoreCase);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby, out var resolvedJvm, out _));
        var shardJvm = resolvedJvm;

        var referenceJvm = shardJvm;
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{referenceJvm}.json");
        if (!File.Exists(referencePath))
        {
            referenceJvm = jvm;
            referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{referenceJvm}.json");
        }

        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        if (!GeometryIrMeshWalk.TryCollectBakedWorldTranslations(
                reference.RootElement, out _, out var refWorldFail))
        {
            Assert.Fail($"{referenceJvm}: reference worldPose: {refWorldFail}");
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            Profile26,
            0f,
            0f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: false));

        var shardPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{shardJvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(shardJvm, shard.RootElement);
        var (atlasW, atlasH) = ResolveParityAtlasDimensions(jvm, texturePath);

        var emitOptions = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = shardJvm };
        var lerCmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh!,
            emitOptions,
            tolerance: 0.35);
        Assert.True(lerCmp.IsMatch, $"{referenceJvm} ({texturePath}) ler: {lerCmp.Message}");
    }

    private static (int atlasW, int atlasH) ResolveParityAtlasDimensions(string jvm, string texturePath)
    {
        if (jvm.Contains("polarbear", StringComparison.OrdinalIgnoreCase))
        {
            return (128, 64);
        }

        if (jvm.Contains("creeper", StringComparison.OrdinalIgnoreCase))
        {
            return (64, 32);
        }

        _ = texturePath;
        return (64, 64);
    }

    [Fact]
    public void Cow_geometry_ir_uses_column_pose_stack_root_ler_for_baked_mesh_transform_semantics()
    {
        Assert.False(CleanRoomEntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
            "net.minecraft.client.model.animal.cow.CowModel",
            "cow",
            "assets/minecraft/textures/entity/cow/cow_temperate.png"));
        Assert.Equal(
            CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot,
            CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
                "net.minecraft.client.model.animal.cow.CowModel",
                "cow",
                "assets/minecraft/textures/entity/cow/cow_temperate.png"));
    }

    private static (JsonDocument? reference, JsonDocument? ir) LoadPair(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return (null, null);
        }

        var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            reference.Dispose();
            return (null, null);
        }

        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            reference.Dispose();
            return (null, null);
        }

        return (reference, JsonDocument.Parse(File.ReadAllText(irPath)));
    }
}
