using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Block-linked object entities (boat, chest boat, chest, minecart, banner, bell, bed, sign) use bytecode IR
/// or hand-lift with object-entity JVM routing and LER skip — not mob LivingEntityRenderer basis.
/// </summary>
public sealed partial class ObjectEntityBlockStateParityTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Theory]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/oak.png", "net.minecraft.client.model.object.boat.BoatModel")]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/bamboo.png", "net.minecraft.client.model.object.boat.RaftModel")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/oak.png", "net.minecraft.client.model.object.boat.BoatModel.createChestBoatModel")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/bamboo.png", "net.minecraft.client.model.object.boat.RaftModel.createChestRaftModel")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal.png", "net.minecraft.client.model.object.chest.ChestModel")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal_left.png", "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyLeftLayer")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal_right.png", "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyRightLayer")]
    [InlineData("Minecart", "assets/minecraft/textures/entity/minecart/minecart.png", "net.minecraft.client.model.object.cart.MinecartModel")]
    [InlineData("Bell", "assets/minecraft/textures/entity/bell/bell_body.png", "net.minecraft.client.model.object.bell.BellModel")]
    [InlineData("BannerFlagStanding", "assets/minecraft/textures/entity/banner/stripe_top.png", "net.minecraft.client.model.object.banner.BannerFlagModel.standingPreviewComposite")]
    [InlineData("BannerFlagWall", "assets/minecraft/textures/entity/banner/banner_base.png", "net.minecraft.client.model.object.banner.BannerFlagModel.wallPreviewComposite")]
    [InlineData("Skull", "assets/minecraft/textures/entity/decorated_pot/skull_pottery_pattern.png", "net.minecraft.client.model.object.skull.SkullModel.previewComposite")]
    [InlineData("DecoratedPotEntity", "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png", "net.minecraft.client.model.DecoratedPotModel.previewComposite")]
    [InlineData("Bed", "assets/minecraft/textures/entity/bed/red.png", "net.minecraft.client.model.BedModel.previewComposite")]
    public void ObjectEntityJvmMap_resolves_expected_mesh_host(string builderMethod, string path, string expectedJvm)
    {
        var candidates = GeometryIrParityObjectEntityJvmMap.EnumerateCandidates(builderMethod, path).ToList();
        Assert.NotEmpty(candidates);
        Assert.Equal(expectedJvm, candidates[0]);
    }

    [Fact]
    public void Bell_resolves_two_part_tree_from_bytecode_shard()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var bodyMin, out var bodyMax);
        Assert.Equal(5f, bodyMin.X, 0.2f);
        Assert.Equal(-13f, bodyMin.Y, 0.2f);
        Assert.Equal(5f, bodyMin.Z, 0.2f);
        Assert.True(bodyMax.Y > bodyMin.Y, "bell body should span positive height after object-entity Y-up correction");
    }

    [Fact]
    public void Bell_preview_dome_extends_below_mounting_flange()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        TransformWorldCorners(model.Elements[0], out var bodyMin, out var bodyMax);
        TransformWorldCorners(model.Elements[1], out var baseMin, out var baseMax);
        Assert.True(
            bodyMin.Y < baseMin.Y - 2f,
            $"bell dome should hang below the top flange; bodyMinY={bodyMin.Y:G3} baseMinY={baseMin.Y:G3}");
        Assert.True(
            bodyMax.Y <= baseMax.Y + 0.5f,
            $"mounting flange should cap the dome; bodyMaxY={bodyMax.Y:G3} baseMaxY={baseMax.Y:G3}");
    }

    [Fact]
    public void Skull_resolves_head_and_hat_layers_with_block_offset()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/skull_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var headMin, out _);
        Assert.Equal(0f, headMin.Y, 0.2f);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/conduit/base.png", 1, 6f)]
    [InlineData("assets/minecraft/textures/entity/conduit/cage.png", 1, 8f)]
    [InlineData("assets/minecraft/textures/entity/conduit/break_particle.png", 1, 6f)]
    [InlineData("assets/minecraft/textures/entity/conduit/wind.png", 1, 16f)]
    public void Conduit_resolves_one_layer_cuboid_from_javap_hand_lift_shard(string path, int expectedElements, float expectedSpan)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(expectedElements, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var min, out var max);
        Assert.Equal(expectedSpan, max.X - min.X, 0.08f);
        Assert.Equal(expectedSpan, max.Y - min.Y, 0.08f);
        Assert.Equal(expectedSpan, max.Z - min.Z, 0.08f);
        Assert.Equal(8f, (min.X + max.X) * 0.5f, 0.08f);
        Assert.Equal(8f, (min.Y + max.Y) * 0.5f, 0.08f);
        Assert.Equal(8f, (min.Z + max.Z) * 0.5f, 0.08f);
    }

    [Fact]
    public void ExperienceOrb_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/experience/experience_orb.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void FishingHook_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/fishing/fishing_hook.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void DragonFireball_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/enderdragon/dragon_fireball.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void BeaconBeam_resolves_two_beam_segments_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/beacon/beacon_beam.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
    }

    [Fact]
    public void GuardianBeam_resolves_single_beam_segment_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/guardian/guardian_beam.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void EndPortalSurface_resolves_single_sheet_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/end_portal/end_portal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png")]
    [InlineData("assets/minecraft/textures/entity/conduit/break_particle.png")]
    [InlineData("assets/minecraft/textures/entity/experience/experience_orb.png")]
    [InlineData("assets/minecraft/textures/entity/enderdragon/dragon_fireball.png")]
    public void HandLift_object_entity_paths_skip_living_entity_renderer_basis(string path)
    {
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.ConduitRenderer.createShellLayer",
            stemLower: "break_particle",
            normalizedAssetPath: path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bed/red.png", "red")]
    [InlineData("assets/minecraft/textures/entity/signs/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/banner/stripe_top.png", "stripe_top")]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/minecart/minecart.png", "minecart")]
    public void ObjectEntity_paths_skip_living_entity_renderer_basis(string path, string stem)
    {
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.BedModel",
            stemLower: stem,
            normalizedAssetPath: path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
        Assert.False(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, stem), path);
    }

    [Fact]
    public void ArmorStand_resolves_standard_living_entity_renderer_basis_despite_object_jvm_package()
    {
        const string path = "assets/minecraft/textures/entity/armorstand/armorstand.png";
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(jvm, "armorstand", path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, basis);
        Assert.True(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, "armorstand"));
        Assert.True(EntityModelRuntime.UsesLivingEntityRendererDespiteObjectPackage(jvm, path));
    }

    [Fact]
    public void ArmorStand_geometry_ir_mesh_folds_living_entity_renderer_and_orients_upright()
    {
        const string path = "assets/minecraft/textures/entity/armorstand/armorstand.png";
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(mesh.UsesLivingEntityRendererColumnYFlip, path);

        var stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, path, stem, isBaby: false, out var resolvedJvm, out var geometryRoot));
        Assert.Equal(jvm, resolvedJvm);
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                OfficialJvmName = jvm,
                AtlasWidth = 64,
                AtlasHeight = 64,
            });

        float headY = 0f;
        var headCount = 0;
        float plateY = 0f;
        var plateCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var centroidY = MeasureElementPreviewCentroidY(mesh.Elements[i]);
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("stick", StringComparison.OrdinalIgnoreCase))
            {
                headY += centroidY;
                headCount++;
            }

            if (partId.Contains("base_plate", StringComparison.OrdinalIgnoreCase))
            {
                plateY += centroidY;
                plateCount++;
            }
        }

        Assert.True(headCount > 0 && plateCount > 0);
        headY /= headCount;
        plateY /= plateCount;
        Assert.True(
            headY > plateY,
            $"armor stand should be upright: headY={headY:F3} plateY={plateY:F3}");

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            if (!string.Equals(partId, "head", StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[i];
            var extentX = MathF.Abs(el.To[0] - el.From[0]);
            var extentY = MathF.Abs(el.To[1] - el.From[1]);
            Assert.InRange(extentX, 1.5f, 2.5f);
            Assert.InRange(extentY, 6.5f, 7.5f);
        }
    }

    private static float MeasureElementPreviewCentroidY(ModelElement el)
    {
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
        var sumY = 0f;
        foreach (var (x, y, z) in corners)
        {
            var world = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            sumY += world.Y / 16f - 0.5f;
        }

        return sumY / corners.Length;
    }

    private static void AssertWorldCornerSetsMatchSingleCorner(Vector3 actual, Vector3 expected, float tolerance)
    {
        Assert.True(Vector3.Distance(actual, expected) <= tolerance,
            $"corner delta {Vector3.Distance(actual, expected):G6} exceeds {tolerance:G6}");
    }

    private static void AssertWorldCornerSetsMatch(MergedJavaBlockModel expected, MergedJavaBlockModel actual, float tolerance)
    {
        var expectedCorners = CollectWorldCorners(expected).OrderBy(CornerSortKey).ToList();
        var actualCorners = CollectWorldCorners(actual).OrderBy(CornerSortKey).ToList();
        Assert.Equal(expectedCorners.Count, actualCorners.Count);
        for (var i = 0; i < expectedCorners.Count; i++)
        {
            var delta = Vector3.Distance(expectedCorners[i], actualCorners[i]);
            Assert.True(delta <= tolerance, $"corner {i} delta {delta:G6} exceeds {tolerance:G6}");
        }
    }

    private static bool IsStandingSignBoardElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 20f && MathF.Abs(height - 12f) < 0.01f;
    }

    private static bool IsStandingSignPostElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width < 5f && MathF.Abs(height - 14f) < 0.01f;
    }
}
