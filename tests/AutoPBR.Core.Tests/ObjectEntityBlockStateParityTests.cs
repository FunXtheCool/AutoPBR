using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;
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

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chest/normal.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_left.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_right.png")]
    public void ChestEntity_lid_and_lock_use_ordered_depth_layers_for_closed_overlap(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                path,
                Profile26,
                0f,
                0f,
                out var model,
                pairDoubleChestPreviewHalves: false),
            path);

        Assert.Equal(3, model.Elements.Count);
        Assert.Equal(PreviewDepthLayerKind.Base, model.Elements[0].DepthLayerKind);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, model.Elements[1].DepthLayerKind);
        Assert.Equal(1, model.Elements[1].LayerOrdinal);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, model.Elements[2].DepthLayerKind);
        Assert.Equal(2, model.Elements[2].LayerOrdinal);
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
        TransformWorldCorners(model.Elements[0], out var bodyMin, out var bodyMax);
        Assert.Equal(5f, bodyMin.X, 0.2f);
        Assert.Equal(-13f, bodyMin.Y, 0.2f);
        Assert.Equal(5f, bodyMin.Z, 0.2f);
        Assert.True(bodyMax.Y > bodyMin.Y, "bell body should span positive height after object-entity Y-up correction");
    }

    [Fact]
    public void Bell_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Bell",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
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
    public void BannerStanding_resolves_flag_bar_and_pole_from_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/banner/white.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
    }

    [Fact]
    public void BannerStanding_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/banner/white.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "BannerFlagStanding",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void BannerStanding_flag_hangs_below_bar_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/banner/white.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        ModelElement? flag = null;
        ModelElement? bar = null;
        foreach (var el in model.Elements)
        {
            var height = el.To[1] - el.From[1];
            if (height > 30f)
            {
                flag = el;
            }
            else if (height is >= 1.5f and <= 3f)
            {
                bar = el;
            }
        }

        Assert.NotNull(flag);
        Assert.NotNull(bar);
        TransformWorldCorners(flag!, out var flagMin, out _);
        TransformWorldCorners(bar!, out _, out var barMax);
        Assert.True(
            flagMin.Y < barMax.Y - 4f,
            $"banner cloth should hang below the bar; flagMinY={flagMin.Y:G3} barMaxY={barMax.Y:G3}");
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
    public void BannerWall_flag_hangs_below_bar_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/banner/banner_base.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        ModelElement? flag = null;
        ModelElement? bar = null;
        foreach (var el in model.Elements)
        {
            var height = el.To[1] - el.From[1];
            if (height > 30f)
            {
                flag = el;
            }
            else if (height is >= 1.5f and <= 3f)
            {
                bar = el;
            }
        }

        Assert.NotNull(flag);
        Assert.NotNull(bar);
        TransformWorldCorners(flag!, out var flagMin, out _);
        TransformWorldCorners(bar!, out _, out var barMax);
        Assert.True(
            flagMin.Y < barMax.Y - 4f,
            $"wall banner cloth should hang below the bar; flagMinY={flagMin.Y:G3} barMaxY={barMax.Y:G3}");
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
    public void StandingSign_board_sits_above_post_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/signs/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        ModelElement? board = null;
        ModelElement? post = null;
        foreach (var el in model.Elements)
        {
            if (IsStandingSignBoardElement(el))
            {
                board = el;
            }
            else if (IsStandingSignPostElement(el))
            {
                post = el;
            }
        }

        Assert.NotNull(board);
        Assert.NotNull(post);
        TransformWorldCorners(board!, out var boardMin, out var boardMax);
        TransformWorldCorners(post!, out var postMin, out var postMax);
        Assert.True(boardMax.Y > postMin.Y + 8f, $"board should sit above post base; boardMaxY={boardMax.Y:G3} postMinY={postMin.Y:G3}");
        var jointGap = MathF.Abs(boardMin.Y - postMax.Y);
        Assert.True(
            jointGap <= 0.05f,
            $"board/post joint separated by {jointGap:G4} texels (boardMinY={boardMin.Y:G4} postMaxY={postMax.Y:G4})");
    }

    [Fact]
    public void StandingSign_baked_vertices_stay_within_element_world_bounds()
    {
        const string path = "assets/minecraft/textures/entity/signs/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        var maxSlop = 0f;
        foreach (var el in mesh.Elements)
        {
            var single = new MergedJavaBlockModel
            {
                Elements = [el],
                Textures = mesh.Textures,
            };
            Assert.True(MinecraftModelBaker.TryBake(single, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
            TransformWorldCorners(el, out var elMin, out var elMax);
            const int stride = MinecraftModelBaker.FloatsPerVertex;
            const float pad = 0.05f;
            elMin -= new Vector3(pad);
            elMax += new Vector3(pad);
            for (var vi = 0; vi < verts.Length; vi += stride)
            {
                var px = verts[vi] * 16f + 8f;
                var py = verts[vi + 1] * 16f + 8f;
                var pz = verts[vi + 2] * 16f + 8f;
                var dx = MathF.Max(0f, MathF.Max(elMin.X - px, px - elMax.X));
                var dy = MathF.Max(0f, MathF.Max(elMin.Y - py, py - elMax.Y));
                var dz = MathF.Max(0f, MathF.Max(elMin.Z - pz, pz - elMax.Z));
                maxSlop = MathF.Max(maxSlop, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSlop <= 0.25f, $"detached baked verts maxSlop={maxSlop:F3} texels");
    }

    [Fact]
    public void StandingSign_gpu_bind_baked_vertices_stay_within_element_world_bounds()
    {
        const string path = "assets/minecraft/textures/entity/signs/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        var maxSlop = 0f;
        foreach (var el in mesh.Elements)
        {
            var single = new MergedJavaBlockModel
            {
                Elements = [el],
                Textures = mesh.Textures,
            };
            Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
                single, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
            TransformWorldCorners(el, out var elMin, out var elMax);
            const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
            const float pad = 0.05f;
            elMin -= new Vector3(pad);
            elMax += new Vector3(pad);
            for (var vi = 0; vi < verts.Length; vi += stride)
            {
                var px = verts[vi];
                var py = verts[vi + 1];
                var pz = verts[vi + 2];
                var dx = MathF.Max(0f, MathF.Max(elMin.X - px, px - elMax.X));
                var dy = MathF.Max(0f, MathF.Max(elMin.Y - py, py - elMax.Y));
                var dz = MathF.Max(0f, MathF.Max(elMin.Z - pz, pz - elMax.Z));
                maxSlop = MathF.Max(maxSlop, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSlop <= 0.25f, $"detached gpu bind verts maxSlop={maxSlop:F3} texels");
    }

    [Fact]
    public void StandingSign_geometry_ir_matches_cleanroom_after_vertical_flip()
    {
        const string path = "assets/minecraft/textures/entity/signs/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var ir, out var prov));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, prov.Kind);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "StandingSignEntity", path, Profile26, out var hand));
        Assert.Equal(hand.Elements.Count, ir.Elements.Count);
        for (var i = 0; i < hand.Elements.Count; i++)
        {
            Assert.True(
                MatricesClose(hand.Elements[i].LocalToParent, ir.Elements[i].LocalToParent, 1e-3f),
                $"element {i} LTP mismatch");
            for (var c = 0; c < 3; c++)
            {
                Assert.Equal(hand.Elements[i].From[c], ir.Elements[i].From[c]);
                Assert.Equal(hand.Elements[i].To[c], ir.Elements[i].To[c]);
            }
        }
    }

    [Fact]
    public void StandingSign_rebaked_mesh_post_bottom_cap_stays_attached()
    {
        const string path = "assets/minecraft/textures/entity/signs/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _), path);
        var post = mesh.Elements.Single(IsStandingSignPostElement);

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = path,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            OrderedTextureZipPaths = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft").ToArray()
        };
        var materials = rebake.OrderedTextureZipPaths.Select(_ => CreatePreviewMaps(64, 32)).ToArray();
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake, materials, 0f, out var verts, out _, out _, applyGeometryIrSetupAnimMotion: false));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        TransformWorldCorners(post, out var postMin, out var postMax);
        var postBottomY = postMin.Y;
        var postSideY = new List<float>();
        var capY = new List<float>();
        foreach (var vi in Enumerable.Range(0, verts!.Length / stride))
        {
            var baseIdx = vi * stride;
            var u = verts[baseIdx + 6];
            var v = verts[baseIdx + 7];
            var y = verts[baseIdx + 1];
            // StandingSignRenderer.createSignLayer stick: texOffs(0, 14) on 64×32 — down cap samples near (0,14).
            var isCapUv = u >= 0f - 0.001f && u <= 2f / 64f + 0.001f &&
                          v >= 14f / 32f - 0.001f && v <= 16f / 32f + 0.001f;
            if (isCapUv)
            {
                capY.Add(y);
            }
            else if (MathF.Abs(u - 2f / 64f) < 0.02f || MathF.Abs(u - 0f) < 0.001f)
            {
                if (MathF.Abs(y - postBottomY) > 0.5f)
                {
                    postSideY.Add(y);
                }
            }
        }

        Assert.True(capY.Count >= 4, $"expected post cap verts, got {capY.Count}");
        Assert.True(postSideY.Count >= 4, $"expected post side verts, got {postSideY.Count}");
        var capMax = capY.Max();
        var sideMin = postSideY.Min();
        Assert.True(
            capMax >= sideMin - 0.02f,
            $"post bottom cap detached: capMaxY={capMax:F4} sideMinY={sideMin:F4} gap={sideMin - capMax:F4}");
    }

    [Fact]
    public void HangingSign_resolves_board_and_four_chain_cuboids_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(5, model.Elements.Count);
        Assert.Single(model.Elements, IsHangingSignBoardElement);
        Assert.Equal(4, model.Elements.Count(IsHangingSignChainElement));
    }

    [Fact]
    public void HangingSign_board_meets_chain_tops_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var board = model.Elements.Single(IsHangingSignBoardElement);
        var chains = model.Elements.Where(IsHangingSignChainElement).ToList();
        Assert.Equal(4, chains.Count);
        TransformWorldCorners(board, out _, out var boardMax);
        var chainBottomY = chains.Min(c =>
        {
            TransformWorldCorners(c, out var min, out _);
            return min.Y;
        });
        var jointGap = MathF.Abs(boardMax.Y - chainBottomY);
        Assert.True(
            jointGap <= 0.75f,
            $"board/chain joint separated by {jointGap:G4} texels (boardMaxY={boardMax.Y:G4} chainBottomY={chainBottomY:G4})");
    }

    [Fact]
    public void HangingSign_geometry_ir_matches_cleanroom_after_vertical_flip()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var ir, out var prov));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, prov.Kind);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "HangingSignEntity", path, Profile26, out var hand));
        Assert.Equal(hand.Elements.Count, ir.Elements.Count);
        for (var i = 0; i < hand.Elements.Count; i++)
        {
            Assert.True(
                MatricesClose(hand.Elements[i].LocalToParent, ir.Elements[i].LocalToParent, 1e-3f),
                $"element {i} LTP mismatch");
            for (var c = 0; c < 3; c++)
            {
                Assert.Equal(hand.Elements[i].From[c], ir.Elements[i].From[c]);
                Assert.Equal(hand.Elements[i].To[c], ir.Elements[i].To[c]);
            }
        }
    }

    [Fact]
    public void HangingSign_wall_resolves_board_and_plank()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.Wall);
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);
        Assert.Single(model.Elements, IsHangingSignBoardElement);
        Assert.Single(model.Elements, IsHangingSignWallPlankElement);
        Assert.Equal(4, model.Elements.Count(IsHangingSignChainElement));
    }

    [Fact]
    public void HangingSign_ceiling_middle_resolves_board_and_vertical_chain_sheet()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.CeilingMiddle);
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        Assert.Single(model.Elements, IsHangingSignBoardElement);
        Assert.Single(model.Elements, IsHangingSignVerticalChainElement);
    }

    [Fact]
    public void HangingSign_wall_geometry_ir_matches_cleanroom_after_vertical_flip()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.Wall);
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var ir, out var prov));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, prov.Kind);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "HangingSignEntity", path, Profile26, out var hand));
        Assert.Equal(hand.Elements.Count, ir.Elements.Count);
        for (var i = 0; i < hand.Elements.Count; i++)
        {
            Assert.True(
                MatricesClose(hand.Elements[i].LocalToParent, ir.Elements[i].LocalToParent, 1e-3f),
                $"element {i} LTP mismatch");
            for (var c = 0; c < 3; c++)
            {
                Assert.Equal(hand.Elements[i].From[c], ir.Elements[i].From[c]);
                Assert.Equal(hand.Elements[i].To[c], ir.Elements[i].To[c]);
            }
        }
    }

    [Fact]
    public void HangingSign_ceiling_middle_geometry_ir_matches_cleanroom_after_vertical_flip()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.CeilingMiddle);
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var ir, out var prov));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, prov.Kind);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "HangingSignEntity", path, Profile26, out var hand));
        Assert.Equal(hand.Elements.Count, ir.Elements.Count);
        for (var i = 0; i < hand.Elements.Count; i++)
        {
            Assert.True(
                MatricesClose(hand.Elements[i].LocalToParent, ir.Elements[i].LocalToParent, 1e-3f),
                $"element {i} LTP mismatch");
            for (var c = 0; c < 3; c++)
            {
                Assert.Equal(hand.Elements[i].From[c], ir.Elements[i].From[c]);
                Assert.Equal(hand.Elements[i].To[c], ir.Elements[i].To[c]);
            }
        }
    }

    [Fact]
    public void HangingSign_baked_vertices_stay_within_element_world_bounds()
    {
        const string path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        var maxSlop = 0f;
        foreach (var el in mesh.Elements)
        {
            var single = new MergedJavaBlockModel
            {
                Elements = [el],
                Textures = mesh.Textures,
            };
            Assert.True(MinecraftModelBaker.TryBake(single, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
            TransformWorldCorners(el, out var elMin, out var elMax);
            const int stride = MinecraftModelBaker.FloatsPerVertex;
            const float pad = 0.05f;
            elMin -= new Vector3(pad);
            elMax += new Vector3(pad);
            for (var vi = 0; vi < verts.Length; vi += stride)
            {
                var px = verts[vi] * 16f + 8f;
                var py = verts[vi + 1] * 16f + 8f;
                var pz = verts[vi + 2] * 16f + 8f;
                var dx = MathF.Max(0f, MathF.Max(elMin.X - px, px - elMax.X));
                var dy = MathF.Max(0f, MathF.Max(elMin.Y - py, py - elMax.Y));
                var dz = MathF.Max(0f, MathF.Max(elMin.Z - pz, pz - elMax.Z));
                maxSlop = MathF.Max(maxSlop, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSlop <= 0.35f, $"detached baked verts maxSlop={maxSlop:F3} texels");
    }

    private static PreviewTextureMaps CreatePreviewMaps(int width, int height) => new()
    {
        Width = width,
        Height = height,
        DiffuseRgba = new byte[width * height * 4],
        NormalRgba = new byte[width * height * 4],
        SpecularRgba = new byte[width * height * 4],
        HeightRgba = new byte[width * height * 4],
    };

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
    public void DecoratedPot_world_bounds_match_javap_closed_pot_assembly()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var corners = CollectWorldCorners(model).ToList();
        var minX = corners.Min(c => c.X);
        var maxX = corners.Max(c => c.X);
        var minY = corners.Min(c => c.Y);
        var maxY = corners.Max(c => c.Y);
        var minZ = corners.Min(c => c.Z);
        var maxZ = corners.Max(c => c.Z);
        Assert.InRange(maxX - minX, 0f, 17f);
        Assert.InRange(maxZ - minZ, 0f, 17f);
        Assert.True(maxY - minY < 42f, $"height span {maxY - minY:G3} exceeds javap pot envelope");
        Assert.True(minY < 4f, $"expected neck Rx(PI) to pull geometry down; minY={minY:G3}");
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
        AssertWorldCornerSetsMatch(cleanMesh, irMesh, tolerance: 0.08f);
    }

    [Fact]
    public void DecoratedPot_neck_pose_hoists_geometry_above_flat_hand_lift_origin()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            minY = MathF.Min(minY, cMin.Y);
            maxY = MathF.Max(maxY, cMax.Y);
        }

        Assert.True(maxY - minY > 12f, $"expected closed pot height span; got {maxY - minY:G3}");
        Assert.True(maxY > minY + 8f, $"expected upright closed pot; minY={minY:G3} maxY={maxY:G3}");
    }

    [Fact]
    public void DecoratedPot_hand_lift_shard_cuboids_match_javap_reference_tree()
    {
        const string jvm = "net.minecraft.client.model.DecoratedPotModel.previewComposite";
        Assert.True(ParityCatalogHandLiftGeometryIrCatalog.TryGetOkRoot(jvm, out var shard));
        using var reference = BuildDecoratedPotJavapReferenceRoot();
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement,
            shard,
            tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void DecoratedPot_baked_vertices_stay_within_element_world_bounds()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        Assert.Equal(2, ordered.Count);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase)
                ? (32, 32)
                : (16, 16);
        }

        var maxSlop = 0f;
        foreach (var el in mesh.Elements)
        {
            var single = new MergedJavaBlockModel
            {
                Elements = [el],
                Textures = mesh.Textures,
            };
            Assert.True(MinecraftModelBaker.TryBake(single, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
            TransformWorldCorners(el, out var elMin, out var elMax);
            const int stride = MinecraftModelBaker.FloatsPerVertex;
            const float pad = 0.05f;
            elMin -= new Vector3(pad);
            elMax += new Vector3(pad);
            for (var vi = 0; vi < verts.Length; vi += stride)
            {
                var px = verts[vi] * 16f + 8f;
                var py = verts[vi + 1] * 16f + 8f;
                var pz = verts[vi + 2] * 16f + 8f;
                var dx = MathF.Max(0f, MathF.Max(elMin.X - px, px - elMax.X));
                var dy = MathF.Max(0f, MathF.Max(elMin.Y - py, py - elMax.Y));
                var dz = MathF.Max(0f, MathF.Max(elMin.Z - pz, pz - elMax.Z));
                maxSlop = MathF.Max(maxSlop, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSlop <= 0.35f, $"detached baked verts maxSlop={maxSlop:F3} texels");
    }

    private static JsonDocument BuildDecoratedPotJavapReferenceRoot()
    {
        const float pi = 3.141592654f;
        var doc = new JsonObject
        {
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "root",
                    ["pose"] = new JsonObject
                    {
                        ["translation"] = new JsonArray { 0, 0, 0 },
                        ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
                    },
                    ["cuboids"] = new JsonArray(),
                    ["children"] = new JsonArray
                    {
                        RefPart("neck", 0, 37, 16, pi, 0, 0,
                            RefCuboid(4, 17, 4, 12, 20, 12, 0, 0),
                            RefCuboid(5, 20, 5, 11, 21, 11, 0, 5)),
                        RefPart("top", 1, 16, 1, 0, 0, 0, RefCapCuboid()),
                        RefPart("bottom", 1, 0, 1, 0, 0, 0, RefCapCuboid()),
                        RefPart("back", 15, 16, 1, 0, 0, pi, RefSideCuboid()),
                        RefPart("left", 1, 16, 1, 0, -pi / 2f, pi, RefSideCuboid()),
                        RefPart("right", 15, 16, 15, 0, pi / 2f, pi, RefSideCuboid()),
                        RefPart("front", 1, 16, 15, pi, 0, 0, RefSideCuboid()),
                    }
                }
            }
        };
        return JsonDocument.Parse(doc.ToJsonString());
    }

    private static JsonObject RefPart(
        string id,
        float tx, float ty, float tz,
        float rx, float ry, float rz,
        params JsonObject[] cuboids)
    {
        var cuboidArray = new JsonArray();
        foreach (var c in cuboids)
        {
            cuboidArray.Add(c);
        }

        return new JsonObject
        {
            ["id"] = id,
            ["pose"] = new JsonObject
            {
                ["translation"] = new JsonArray { tx, ty, tz },
                ["rotationEulerRad"] = new JsonArray { rx, ry, rz },
            },
            ["cuboids"] = cuboidArray,
            ["children"] = new JsonArray()
        };
    }

    private static JsonObject RefCuboid(
        float x0, float y0, float z0, float x1, float y1, float z1, int u, int v,
        int? uvW = null, int? uvH = null, int? uvD = null)
    {
        var c = new JsonObject
        {
            ["from"] = new JsonArray { x0, y0, z0 },
            ["to"] = new JsonArray { x1, y1, z1 },
            ["uvOrigin"] = new JsonArray { u, v },
        };
        if (uvW is not null && uvH is not null && uvD is not null)
        {
            c["uvSpan"] = new JsonArray { uvW.Value, uvH.Value, uvD.Value };
        }

        return c;
    }

    private static JsonObject RefSideCuboid() => RefCuboid(0, 0, 0, 14, 16, 0, 1, 0, 14, 16, 0);

    private static JsonObject RefCapCuboid()
    {
        var c = RefCuboid(0, 0, 0, 14, 0, 14, -14, 13, 14, 0, 14);
        c["faceMask"] = new JsonArray { "down" };
        return c;
    }

    [Fact]
    public void DecoratedPot_preview_orientation_has_neck_above_base_ring()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var neckMaxY = float.MinValue;
        var bottomCapMaxY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isCap = el.Faces.TryGetValue("up", out var upFace) &&
                        upFace.Uv is { Length: 4 } &&
                        upFace.Uv[0] >= 13.5f &&
                        upFace.Uv[2] <= 28.5f &&
                        upFace.Uv[1] >= 12.5f &&
                        upFace.Uv[3] <= 27.5f;
            if (isCap)
            {
                bottomCapMaxY = MathF.Min(bottomCapMaxY, cMax.Y);
            }
            else
            {
                neckMaxY = MathF.Max(neckMaxY, cMax.Y);
            }
        }

        Assert.True(neckMaxY > bottomCapMaxY + 4f,
            $"expected neck above base ring; neckMaxY={neckMaxY:G3} bottomCapMaxY={bottomCapMaxY:G3}");
    }

    [Fact]
    public void DecoratedPot_cap_rings_seal_side_body_seams()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideMinY = float.MaxValue;
        var sideMaxY = float.MinValue;
        var topCapMaxY = float.MinValue;
        var bottomCapMinY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("up"))
            {
                if (cMax.Y > 8f)
                {
                    topCapMaxY = MathF.Max(topCapMaxY, cMax.Y);
                }
                else
                {
                    bottomCapMinY = MathF.Min(bottomCapMinY, cMin.Y);
                }

                continue;
            }

            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("north"))
            {
                sideMinY = MathF.Min(sideMinY, cMin.Y);
                sideMaxY = MathF.Max(sideMaxY, cMax.Y);
            }
        }

        // javap caps stay on y=0/16 planes; sides extend Y into cap rings for vertical rim seal only.
        var topGap = sideMaxY - topCapMaxY;
        var bottomGap = bottomCapMinY - sideMinY;
        Assert.True(topGap <= 0.05f,
            $"top cap should meet side rim; sideMaxY={sideMaxY:G3} topCapMaxY={topCapMaxY:G3} gap={topGap:G3}");
        Assert.True(bottomGap <= 0.05f,
            $"bottom cap should meet side base; bottomCapMinY={bottomCapMinY:G3} sideMinY={sideMinY:G3} gap={bottomGap:G3}");
    }

    [Fact]
    public void DecoratedPot_cap_ring_shoulder_y_aligns_with_side_top_edge()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        float? topCapMaxY = null;
        var sideTopYs = new List<float>();
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("up") &&
                cMax.Y > 8f)
            {
                topCapMaxY = cMax.Y;
                continue;
            }

            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("north"))
            {
                sideTopYs.Add(cMax.Y);
            }
        }

        Assert.NotNull(topCapMaxY);
        Assert.Equal(4, sideTopYs.Count);
        foreach (var sideTopY in sideTopYs)
        {
            Assert.InRange(MathF.Abs(sideTopY - topCapMaxY.Value), 0f, 0.01f);
        }
    }

    [Fact]
    public void DecoratedPot_side_panels_extend_vertically_without_horizontal_protrusion()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        const float minSideHeight = 16f + CleanRoomEntityModelRuntime.DecoratedPotPreviewVerticalSealOverlap - 0.01f;
        const float maxHorizontalSpan = 14f + 0.02f;
        const float maxSideDepth = 0.52f;
        var sideCount = 0;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            sideCount++;
            TransformWorldCorners(el, out var cMin, out var cMax);
            var xSpan = cMax.X - cMin.X;
            var zSpan = cMax.Z - cMin.Z;
            var ySpan = cMax.Y - cMin.Y;
            var horizontalSpan = MathF.Max(xSpan, zSpan);
            var thinSpan = MathF.Min(MathF.Min(xSpan, zSpan), ySpan);
            Assert.InRange(horizontalSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.True(ySpan >= minSideHeight,
                $"side panel height {ySpan:G3} should extend into cap rings (>= {minSideHeight:G3})");
            Assert.True(thinSpan <= maxSideDepth,
                $"side panel depth {thinSpan:G3} should stay within preview inward extrude (<= {maxSideDepth:G3})");
        }

        Assert.Equal(4, sideCount);
    }

    [Fact]
    public void DecoratedPot_body_ring_footprint_matches_javap_without_horizontal_overshoot()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideMinX = float.MaxValue;
        var sideMaxX = float.MinValue;
        var sideMinZ = float.MaxValue;
        var sideMaxZ = float.MinValue;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            sideMinX = MathF.Min(sideMinX, cMin.X);
            sideMaxX = MathF.Max(sideMaxX, cMax.X);
            sideMinZ = MathF.Min(sideMinZ, cMin.Z);
            sideMaxZ = MathF.Max(sideMaxZ, cMax.Z);
        }

        // javap sides: 14-wide sheets on the 1..15 perimeter — no preview widen past those planes.
        Assert.InRange(sideMinX, 0.99f, 1.01f);
        Assert.InRange(sideMaxX, 14.99f, 15.01f);
        Assert.InRange(sideMinZ, 0.99f, 1.01f);
        Assert.InRange(sideMaxZ, 14.99f, 15.01f);
    }

    [Fact]
    public void DecoratedPot_side_exterior_plane_stays_at_local_z0_after_thicken()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideCount = 0;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            sideCount++;
            Assert.InRange(el.From[2], -0.01f, 0.01f);
            Assert.InRange(el.To[2], 0.45f, 0.55f);
        }

        Assert.Equal(4, sideCount);
    }

    [Fact]
    public void DecoratedPot_cap_rings_keep_javap_horizontal_footprint()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        const float maxHorizontalSpan = 14f + 0.02f;
        const float maxCapThickness = 0.52f;
        var capCount = 0;
        foreach (var el in model.Elements)
        {
            if (!el.Faces.TryGetValue("up", out var capFace) ||
                capFace.Uv is not { Length: 4 } ||
                capFace.Uv[0] < 13.5f || capFace.Uv[2] > 28.5f ||
                capFace.Uv[1] < 12.5f || capFace.Uv[3] > 27.5f)
            {
                continue;
            }

            var texKey = capFace.TextureKey ?? "";
            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            capCount++;
            TransformWorldCorners(el, out var cMin, out var cMax);
            var xSpan = cMax.X - cMin.X;
            var zSpan = cMax.Z - cMin.Z;
            var ySpan = cMax.Y - cMin.Y;
            Assert.InRange(xSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.InRange(zSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.True(ySpan <= maxCapThickness,
                $"cap down-face extrude should stay thin (y thickness {ySpan:G3} <= {maxCapThickness:G3})");
        }

        Assert.Equal(2, capCount);
    }

    [Fact]
    public void DecoratedPot_baked_rim_vertices_overlap_side_and_cap_sheets()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sideYs = new List<float>();
        var capTopYs = new List<float>();
        var capBottomYs = new List<float>();
        var floatOffset = 0;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            var el = mesh.Elements[ei];
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            var isCap = texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                        el.Faces.ContainsKey("up");
            var isSide = !texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                         el.Faces.ContainsKey("north");
            if (!isCap && !isSide)
            {
                floatOffset += CountElementFloats(el, stride);
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            var isTopCap = cMax.Y > 8f;
            var vertCount = CountElementFloats(el, stride);
            for (var vi = 0; vi < vertCount / stride; vi++)
            {
                var y = verts[floatOffset + vi * stride + 1];
                if (isCap)
                {
                    if (isTopCap)
                    {
                        capTopYs.Add(y);
                    }
                    else
                    {
                        capBottomYs.Add(y);
                    }
                }
                else
                {
                    sideYs.Add(y);
                }
            }

            floatOffset += vertCount;
        }

        Assert.NotEmpty(sideYs);
        Assert.NotEmpty(capTopYs);
        var sideTopMax = sideYs.Max();
        var sideBottomMin = sideYs.Min();
        var topOverlap = sideTopMax - capTopYs.Min();
        var bottomOverlap = capBottomYs.Max() - sideBottomMin;
        Assert.True(topOverlap >= -0.05f,
            $"top rim should overlap; sideTopMax={sideTopMax:G3} capTopMin={capTopYs.Min():G3} overlap={topOverlap:G3}");
        Assert.True(bottomOverlap >= -0.05f,
            $"bottom rim should overlap; capBottomMax={capBottomYs.Max():G3} sideBottomMin={sideBottomMin:G3} overlap={bottomOverlap:G3}");
    }

    [Fact]
    public void DecoratedPot_baked_top_rim_y_overlap_uses_production_atlas_path()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sideTopYs = new List<float>();
        var capTopYs = new List<float>();
        var floatOffset = 0;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            var el = mesh.Elements[ei];
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            var isCap = texKey.Contains("base", StringComparison.OrdinalIgnoreCase) && el.Faces.ContainsKey("up");
            var isSide = !texKey.Contains("base", StringComparison.OrdinalIgnoreCase) && el.Faces.ContainsKey("north");
            if (!isCap && !isSide)
            {
                floatOffset += CountElementFloats(el, stride);
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            var isTopCap = cMax.Y > 8f;
            var vertCount = CountElementFloats(el, stride);
            for (var vi = 0; vi < vertCount / stride; vi++)
            {
                var y = verts[floatOffset + vi * stride + 1];
                if (isCap && isTopCap)
                {
                    capTopYs.Add(y);
                }
                else if (isSide)
                {
                    sideTopYs.Add(y);
                }
            }

            floatOffset += vertCount;
        }

        Assert.NotEmpty(sideTopYs);
        Assert.NotEmpty(capTopYs);
        var topOverlap = sideTopYs.Max() - capTopYs.Min();
        Assert.True(topOverlap >= -0.02f,
            $"top rim baked verts should overlap; sideTopMax={sideTopYs.Max():G3} capTopMin={capTopYs.Min():G3} overlap={topOverlap:G3}");
    }

    private static int CountElementFloats(ModelElement el, int stride)
    {
        var count = 0;
        foreach (var faceName in new[] { "north", "south", "west", "east", "up", "down" })
        {
            if (el.Faces.ContainsKey(faceName))
            {
                count += 4 * stride;
            }
        }

        return count;
    }

    [Fact]
    public void DecoratedPot_cap_and_side_faces_use_texcrop_uv_on_base_and_pattern()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var capFaces = new List<float[]>();
        var sideFaces = new List<float[]>();
        foreach (var el in model.Elements)
        {
            foreach (var (faceName, face) in el.Faces)
            {
                if (face.Uv is not { Length: 4 })
                {
                    continue;
                }

                var texKey = face.TextureKey ?? "#skin";
                if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                    faceName.Equals("up", StringComparison.OrdinalIgnoreCase) &&
                    face.Uv[0] >= 13.5f && face.Uv[2] <= 28.5f &&
                    face.Uv[1] >= 12.5f && face.Uv[3] <= 27.5f)
                {
                    capFaces.Add(face.Uv);
                }
                else if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                         faceName.Equals("north", StringComparison.OrdinalIgnoreCase))
                {
                    sideFaces.Add(face.Uv);
                }
            }
        }

        Assert.Equal(2, capFaces.Count);
        foreach (var uv in capFaces)
        {
            Assert.InRange(uv[0], 13.9f, 14.1f);
            Assert.InRange(uv[1], 12.9f, 13.1f);
            Assert.InRange(uv[2], 27.9f, 28.1f);
            Assert.InRange(uv[3], 26.9f, 27.1f);
        }

        Assert.Equal(4, sideFaces.Count);
        foreach (var uv in sideFaces)
        {
            Assert.InRange(uv[0], 0.9f, 1.1f);
            Assert.InRange(uv[1], -0.1f, 0.1f);
            Assert.InRange(uv[2], 14.9f, 15.1f);
            Assert.InRange(uv[3], 15.9f, 16.1f);
        }
    }

    [Fact]
    public void DecoratedPot_bake_atlas_uses_javap_per_texture_dimensions()
    {
        const string pattern = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        const string baseTex = "assets/minecraft/textures/entity/decorated_pot/decorated_pot_base.png";
        var provenance = new PreviewMeshProvenance(
            PreviewMeshDriverKind.RuntimeGeometryIrJson,
            "net.minecraft.client.model.DecoratedPotModel.previewComposite");

        Assert.Equal((16, 16), EntityGeometryIrTextureAtlas.ResolveForBake(pattern, 16, 16, provenance));
        Assert.Equal((32, 32), EntityGeometryIrTextureAtlas.ResolveForBake(baseTex, 32, 32, provenance));
        // Manifest rows still declare 64×64; bake must follow javap sheet sizes instead.
        Assert.Equal((16, 16), EntityGeometryIrTextureAtlas.ResolveForBake(pattern, 64, 64, provenance));
        Assert.Equal((32, 32), EntityGeometryIrTextureAtlas.ResolveForBake(baseTex, 64, 64, provenance));
    }

    [Fact]
    public void DecoratedPot_baked_side_normalized_uv_uses_16x16_atlas()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var sideEl = mesh.Elements.First(el =>
            !el.Faces.Values.Any(f => (f.TextureKey ?? "").Contains("base", StringComparison.OrdinalIgnoreCase)) &&
            el.Faces.ContainsKey("north"));
        var floatOffset = 0;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            if (!ReferenceEquals(mesh.Elements[ei], sideEl))
            {
                floatOffset += CountElementFloats(mesh.Elements[ei], stride);
                continue;
            }

            break;
        }

        var sideUs = new List<float>();
        var vertCount = CountElementFloats(sideEl, stride);
        for (var vi = 0; vi < vertCount / stride; vi++)
        {
            sideUs.Add(verts[floatOffset + vi * stride + 6]);
        }

        Assert.InRange(sideUs.Min(), 1f / 16f - 0.02f, 2f / 16f + 0.02f);
        Assert.InRange(sideUs.Max(), 14f / 16f - 0.02f, 15f / 16f + 0.02f);
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
    public void Bed_preview_mattress_sits_above_legs_in_world_space()
    {
        const string path = "assets/minecraft/textures/entity/bed/black.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var mattressYs = new List<float>();
        var legMaxY = float.MinValue;
        foreach (var el in model.Elements)
        {
            var width = el.To[0] - el.From[0];
            var depth = el.To[1] - el.From[1];
            var isMattress = width > 10f && depth > 10f;
            TransformWorldCorners(el, out var min, out var max);
            if (isMattress)
            {
                mattressYs.Add((min.Y + max.Y) * 0.5f);
            }
            else
            {
                legMaxY = MathF.Max(legMaxY, max.Y);
            }
        }

        Assert.Equal(2, mattressYs.Count);
        foreach (var mattressCenterY in mattressYs)
        {
            Assert.True(
                mattressCenterY > legMaxY - 0.5f,
                $"mattress should sit above legs; mattressCenterY={mattressCenterY:G3} legMaxY={legMaxY:G3}");
        }
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

    [Theory]
    [InlineData("assets/minecraft/textures/entity/conduit/base.png")]
    [InlineData("assets/minecraft/textures/entity/conduit/cage.png")]
    public void Conduit_ir_mesh_matches_cleanroom_bind_pose(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "ConduitEntity",
                path,
                Profile26,
                out var cleanMesh),
            path);
        AssertWorldCornerSetsMatch(cleanMesh, irMesh, tolerance: 0.05f);
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
        var basis = CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.ConduitRenderer.createShellLayer",
            stemLower: "break_particle",
            normalizedAssetPath: path);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bed/red.png", "red")]
    [InlineData("assets/minecraft/textures/entity/signs/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/banner/white.png", "white")]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/minecart/minecart.png", "minecart")]
    public void ObjectEntity_paths_skip_living_entity_renderer_basis(string path, string stem)
    {
        var basis = CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.BedModel",
            stemLower: stem,
            normalizedAssetPath: path);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
        Assert.False(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, stem), path);
    }

    [Fact]
    public void ArmorStand_resolves_standard_living_entity_renderer_basis_despite_object_jvm_package()
    {
        const string path = "assets/minecraft/textures/entity/armorstand/armorstand.png";
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var basis = CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(jvm, "armorstand", path);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, basis);
        Assert.True(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, "armorstand"));
        Assert.True(CleanRoomEntityModelRuntime.UsesLivingEntityRendererDespiteObjectPackage(jvm, path));
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

    [Theory]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png")]
    public void Boat_family_static_mesh_does_not_fold_living_entity_renderer_basis(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.False(mesh.UsesLivingEntityRendererColumnYFlip, path);

        var scratch = new List<Matrix4x4>();
        Assert.True(runtime.TryFillBoneMatricesFast(path, Profile26, 0f, 0f, scratch, out var boneCount), path);
        Assert.Equal(mesh.Elements.Count, boneCount);
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(
                MatricesClose(scratch[i], mesh.Elements[i].LocalToParent, 1e-4f),
                $"{path}: bone {i} must match bind LocalToParent (no LER double-apply)");
        }
    }

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps) =>
        MathF.Abs(a.M11 - b.M11) <= eps && MathF.Abs(a.M22 - b.M22) <= eps && MathF.Abs(a.M33 - b.M33) <= eps &&
        MathF.Abs(a.M44 - b.M44) <= eps && MathF.Abs(a.M41 - b.M41) <= eps && MathF.Abs(a.M42 - b.M42) <= eps &&
        MathF.Abs(a.M43 - b.M43) <= eps;

    [Fact]
    public void BoatOak_emit_from_lifted_resolver_root_matches_cleanroom_landmark()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);
        var bottom = FindBoatHullBottomSlab(mesh);
        TransformWorldCorners(bottom, out var min, out _);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Boat",
                path,
                Profile26,
                out var clean),
            path);
        TransformWorldCorners(FindBoatHullBottomSlab(clean), out var cleanMin, out _);
        Assert.True(Vector3.Distance(min, cleanMin) <= 0.05f,
            $"bottom min corner delta {Vector3.Distance(min, cleanMin):G6}");
    }

    private static ModelElement FindBoatHullBottomSlab(MergedJavaBlockModel model)
    {
        foreach (var el in model.Elements)
        {
            var lx = MathF.Abs(el.To[0] - el.From[0]);
            var ly = MathF.Abs(el.To[1] - el.From[1]);
            var lz = MathF.Abs(el.To[2] - el.From[2]);
            if (lx is >= 27f and <= 29f &&
                ly is >= 15f and <= 17f &&
                lz is >= 2.5f and <= 3.5f)
            {
                return el;
            }
        }

        throw new InvalidOperationException("boat hull bottom slab (28×16×3 local) not found");
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
    public void BoatOak_bottom_slab_matches_cleanroom_landmark()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Boat",
                path,
                Profile26,
                out var clean),
            path);
        TransformWorldCorners(FindBoatHullBottomSlab(model), out var irMin, out var irMax);
        TransformWorldCorners(FindBoatHullBottomSlab(clean), out var cleanMin, out var cleanMax);
        AssertWorldCornerSetsMatchSingleCorner(irMin, cleanMin, 0.05f);
        AssertWorldCornerSetsMatchSingleCorner(irMax, cleanMax, 0.05f);
    }

    private static void AssertWorldCornerSetsMatchSingleCorner(Vector3 actual, Vector3 expected, float tolerance)
    {
        Assert.True(Vector3.Distance(actual, expected) <= tolerance,
            $"corner delta {Vector3.Distance(actual, expected):G6} exceeds {tolerance:G6}");
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

    [Fact]
    public void Minecart_ir_mesh_matches_cleanroom_bind_pose()
    {
        const string path = "assets/minecraft/textures/entity/minecart/minecart.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var irMesh), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Minecart",
                path,
                Profile26,
                out var cleanMesh),
            path);
        var cmp = GeometryIrMeshParityComparer.Compare(irMesh, cleanMesh, tolerance: 0.05f);
        Assert.True(cmp.IsMatch, cmp.Message);
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

    private static bool IsHangingSignBoardElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 12f && MathF.Abs(height - 10f) < 0.01f;
    }

    private static bool IsHangingSignChainElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width < 5f && MathF.Abs(height - 6f) < 0.01f;
    }

    private static bool IsHangingSignWallPlankElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 14f && MathF.Abs(height - 2f) < 0.01f;
    }

    private static bool IsHangingSignVerticalChainElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 10f && MathF.Abs(height - 6f) < 0.01f;
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
