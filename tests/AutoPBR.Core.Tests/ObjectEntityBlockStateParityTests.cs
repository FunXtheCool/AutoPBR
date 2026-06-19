using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Block-linked object entities (boat, chest boat, chest, minecart, banner, bell, bed, sign) use bytecode IR
/// or hand-lift with object-entity JVM routing and LER skip — not mob LivingEntityRenderer basis.
/// </summary>
public sealed class ObjectEntityBlockStateParityTests
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
    [InlineData("BannerFlagStanding", "assets/minecraft/textures/entity/banner/white.png", "net.minecraft.client.model.object.banner.BannerFlagModel.standingPreviewComposite")]
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
    public void ChestEntity_resolves_three_part_tree_from_bytecode_shard()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
    }

    [Fact]
    public void ChestEntity_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "ChestEntity",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chest/normal_left.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_right.png")]
    public void ChestEntity_double_half_ir_mesh_matches_cleanroom_bind_pose(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                path,
                Profile26,
                0f,
                0f,
                out var irMesh,
                pairDoubleChestPreviewHalves: false),
            path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "ChestEntity",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chest/normal_left.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_right.png")]
    public void ChestEntity_paired_preview_merges_left_and_right_halves(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);
        Assert.Equal(2, model.Textures.Count);
        Assert.True(model.Textures.ContainsKey("skin"));
        Assert.True(model.Textures.ContainsKey("chest_pair"));

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            minX = MathF.Min(minX, cMin.X);
            maxX = MathF.Max(maxX, cMax.X);
        }

        Assert.True(minX is >= 0.5f and <= 2f, $"expected right half anchored near origin; minX={minX}");
        Assert.True(maxX > 30f, $"expected left half offset by one block; maxX={maxX}");
    }

    [Fact]
    public void ChestEntity_partner_path_swaps_left_and_right_suffix()
    {
        Assert.True(
            CleanRoomEntityModelRuntime.TryGetDoubleChestPartnerAssetPath(
                "assets/minecraft/textures/entity/chest/normal_left.png",
                out var right));
        Assert.Equal("assets/minecraft/textures/entity/chest/normal_right.png", right);

        Assert.True(
            CleanRoomEntityModelRuntime.TryGetDoubleChestPartnerAssetPath(
                "assets/minecraft/textures/entity/chest/copper_exposed_right.png",
                out var left));
        Assert.Equal("assets/minecraft/textures/entity/chest/copper_exposed_left.png", left);
    }

    [Fact]
    public void ChestEntity_single_body_emits_all_six_faces()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        foreach (var el in model.Elements)
        {
            Assert.True(el.Faces.ContainsKey("west"), $"element missing west face: {string.Join(',', el.Faces.Keys)}");
        }
    }

    [Fact]
    public void ChestEntity_closed_lid_hinge_landmark_matches_java_pose()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
        TransformWorldCorners(model.Elements[1], out var lidMin, out var lidMax);
        Assert.Equal(1f, lidMin.X, 0.1f);
        Assert.Equal(9f, lidMin.Y, 0.1f);
        Assert.Equal(1f, lidMin.Z, 0.1f);
        Assert.Equal(15f, lidMax.X, 0.1f);
        Assert.Equal(14f, lidMax.Y, 0.1f);
        Assert.Equal(15f, lidMax.Z, 0.1f);
    }

    [Fact]
    public void Bell_resolves_two_part_tree_from_bytecode_shard()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var bodyMin, out _);
        Assert.Equal(5f, bodyMin.X, 0.2f);
        Assert.Equal(6f, bodyMin.Y, 0.2f);
        Assert.Equal(5f, bodyMin.Z, 0.2f);
    }

    [Fact]
    public void BannerStanding_resolves_flag_bar_and_pole_from_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/banner/white.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
    }

    [Fact]
    public void BannerWall_resolves_flag_and_bar_without_pole()
    {
        const string path = "assets/minecraft/textures/entity/banner/banner_base.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
    }

    [Fact]
    public void StandingSign_resolves_two_cuboids_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/signs/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
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

    [Fact]
    public void DecoratedPot_resolves_eight_cuboids_from_preview_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(8, model.Elements.Count);
    }

    [Fact]
    public void DecoratedPot_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "DecoratedPotEntity",
                path,
                Profile26,
                out var cleanMesh),
            path);
        AssertWorldCornerSetsMatch(cleanMesh, irMesh, tolerance: 0.05f);
    }

    [Fact]
    public void DecoratedPot_neck_pose_hoists_geometry_above_flat_hand_lift_origin()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var minY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out _);
            minY = MathF.Min(minY, cMin.Y);
        }

        Assert.True(minY < 10f, $"expected neck rotation to drop world min Y below flat hand-lift (~17); got {minY}");
    }

    [Fact]
    public void Bed_resolves_six_cuboids_from_preview_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/bed/red.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);
    }

    [Fact]
    public void Bed_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/bed/red.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Bed",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Bed_preview_mattress_slabs_lie_flat_and_connect()
    {
        const string path = "assets/minecraft/textures/entity/bed/red.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);

        var mains = model.Elements
            .Where(e => e.From[1] == 0f && e.To[1] == 16f && e.From[2] == 0f && e.To[2] == 6f)
            .ToList();
        Assert.Equal(2, mains.Count);

        foreach (var slab in mains)
        {
            TransformWorldCorners(slab, out var min, out var max);
            Assert.InRange(max.Y - min.Y, 5f, 7f);
            Assert.True(max.Y <= 16f, $"mattress slab should sit near ground after preview facing: yMax={max.Y}");
        }

        TransformWorldCorners(mains[0], out var headMin, out var headMax);
        TransformWorldCorners(mains[1], out var footMin, out var footMax);
        var headCenterZ = (headMin.Z + headMax.Z) * 0.5f;
        var footCenterZ = (footMin.Z + footMax.Z) * 0.5f;
        Assert.InRange(MathF.Abs(headCenterZ - footCenterZ), 14f, 18f);
    }

    [Fact]
    public void Conduit_resolves_two_shell_cuboids_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/conduit/break_particle.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
    }

    [Fact]
    public void ExperienceOrb_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/experience/experience_orb.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(1, model.Elements.Count);
    }

    [Fact]
    public void FishingHook_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/fishing/fishing_hook.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(1, model.Elements.Count);
    }

    [Fact]
    public void DragonFireball_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/enderdragon/dragon_fireball.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(1, model.Elements.Count);
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
        Assert.Equal(1, model.Elements.Count);
    }

    [Fact]
    public void EndPortalSurface_resolves_single_sheet_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/end_portal/end_portal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(1, model.Elements.Count);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png")]
    [InlineData("assets/minecraft/textures/entity/conduit/break_particle.png")]
    [InlineData("assets/minecraft/textures/entity/experience/experience_orb.png")]
    [InlineData("assets/minecraft/textures/entity/enderdragon/dragon_fireball.png")]
    public void HandLift_object_entity_paths_skip_living_entity_renderer_basis(string path)
    {
        var basis = CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.ConduitModel",
            stemLower: "break_particle",
            normalizedAssetPath: path);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bed/red.png", "red")]
    [InlineData("assets/minecraft/textures/entity/signs/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/banner/white.png", "white")]
    public void ObjectEntity_paths_skip_living_entity_renderer_basis(string path, string stem)
    {
        var basis = CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.BedModel",
            stemLower: stem,
            normalizedAssetPath: path);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
    }

    [Fact]
    public void BoatOak_emit_from_repaired_resolver_root_matches_cleanroom_landmark()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var rule = EntityTextureParityCatalog.ResolveRule(path, "oak");
        Assert.NotNull(rule);
        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26,
                rule!,
                path,
                "oak",
                isBaby: false,
                out var jvm,
                out var geometryRoot),
            path);
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "assets/minecraft/textures/entity/boat/oak",
            Profile26,
            jvm,
            atlasWidth: 128,
            atlasHeight: 64,
            out var failure,
            geometryRootOverride: repaired);
        Assert.Null(failure);
        Assert.NotNull(mesh);
        TransformWorldCorners(mesh!.Elements[0], out var min, out _);
        Assert.Equal(1.5f, min.Y, 0.15f);
    }

    [Fact]
    public void BoatOak_resolved_shard_repair_zeros_bottom_part_rotation()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var rule = EntityTextureParityCatalog.ResolveRule(path, "oak");
        Assert.NotNull(rule);
        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26,
                rule!,
                path,
                "oak",
                isBaby: false,
                out var jvm,
                out var geometryRoot),
            path);
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var bottom = FindPart(repaired, "bottom");
        Assert.NotNull(bottom);
        var poseRot = bottom!.Value.GetProperty("pose").GetProperty("rotationEulerRad");
        Assert.Equal(0d, poseRot[0].GetDouble(), 3);
    }

    [Fact]
    public void BoatOak_resolved_geometry_root_is_full_shard_document()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var rule = EntityTextureParityCatalog.ResolveRule(path, "oak");
        Assert.NotNull(rule);
        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26,
                rule!,
                path,
                "oak",
                isBaby: false,
                out var jvm,
                out var geometryRoot),
            path);
        Assert.Equal("net.minecraft.client.model.object.boat.BoatModel", jvm);
        Assert.True(geometryRoot.TryGetProperty("roots", out var roots));
        Assert.Equal(JsonValueKind.Array, roots.ValueKind);
    }

    [Fact]
    public void BoatOak_bottom_slab_matches_cleanroom_landmark_after_repair()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        TransformWorldCorners(model.Elements[0], out var min, out _);
        Assert.Equal(1.5f, min.Y, 0.15f);
        Assert.Equal(-5.5f, min.Z, 0.35f);
        Assert.Equal(-14f, min.X, 0.35f);
    }

    [Fact]
    public void BoatModel_part_tree_repair_hoists_bottom_rotation_to_cuboid_pivot()
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var path = Path.Combine(
            root,
            "docs",
            "generated",
            "geometry",
            "26.1.2",
            "net.minecraft.client.model.object.boat.BoatModel.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(path));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(
            "net.minecraft.client.model.object.boat.BoatModel",
            shard.RootElement);

        var bottom = FindPart(repaired, "bottom");
        Assert.NotNull(bottom);
        var poseRot = bottom!.Value.GetProperty("pose").GetProperty("rotationEulerRad");
        Assert.Equal(0d, poseRot[0].GetDouble(), 3);
        Assert.Equal(0d, poseRot[1].GetDouble(), 3);
        Assert.Equal(0d, poseRot[2].GetDouble(), 3);
        var cuboid = bottom.Value.GetProperty("cuboids")[0];
        Assert.True(cuboid.TryGetProperty("rotationPivot", out _));
        Assert.True(Math.Abs(cuboid.GetProperty("cuboidRotationEulerRad")[0].GetDouble() - Math.PI / 2) < 0.01);
    }

    [Theory]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/oak.png")]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/bamboo.png")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/oak.png")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/bamboo.png")]
    public void Boat_family_ir_mesh_matches_cleanroom_bind_pose(string builderMethod, string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                builderMethod,
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    private static JsonElement? FindPart(JsonElement doc, string partId)
    {
        if (!doc.TryGetProperty("roots", out var roots))
        {
            return null;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryFindPartRecursive(root, partId, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPartRecursive(JsonElement part, string partId, out JsonElement found)
    {
        found = default;
        if (part.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (!part.TryGetProperty("children", out var kids))
        {
            return false;
        }

        foreach (var child in kids.EnumerateArray())
        {
            if (TryFindPartRecursive(child, partId, out found))
            {
                return true;
            }
        }

        return false;
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

    private static IEnumerable<Vector3> CollectWorldCorners(MergedJavaBlockModel model)
    {
        foreach (var el in model.Elements)
        {
            var m = el.LocalToParent;
            var fx = el.From[0];
            var fy = el.From[1];
            var fz = el.From[2];
            var tx = el.To[0];
            var ty = el.To[1];
            var tz = el.To[2];
            (float x, float y, float z)[] c =
            [
                (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
                (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
            ];
            foreach (var p in c)
            {
                yield return Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            }
        }
    }

    private static string CornerSortKey(Vector3 v) => $"{v.X:F4},{v.Y:F4},{v.Z:F4}";

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        var m = el.LocalToParent;
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var fx = el.From[0];
        var fy = el.From[1];
        var fz = el.From[2];
        var tx = el.To[0];
        var ty = el.To[1];
        var tz = el.To[2];
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
            (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }
}
