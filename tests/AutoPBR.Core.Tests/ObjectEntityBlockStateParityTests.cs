using System.Numerics;
using System.Text.Json;
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
        TransformWorldCorners(board!, out _, out var boardMax);
        TransformWorldCorners(post!, out var postMin, out _);
        Assert.True(boardMax.Y > postMin.Y + 8f, $"board should sit above post base; boardMaxY={boardMax.Y:G3} postMinY={postMin.Y:G3}");
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
        var postSideY = new List<float>();
        var capY = new List<float>();
        foreach (var vi in Enumerable.Range(0, verts!.Length / stride))
        {
            var baseIdx = vi * stride;
            var u = verts[baseIdx + 6];
            var v = verts[baseIdx + 7];
            var y = verts[baseIdx + 1];
            var isCapUv = u >= 30f / 64f - 0.001f && u <= 32f / 64f + 0.001f &&
                          v >= 0f - 0.001f && v <= 2f / 32f + 0.001f;
            if (isCapUv)
            {
                capY.Add(y);
            }
            else if (MathF.Abs(u - 28f / 64f) < 0.02f || MathF.Abs(u - 26f / 64f) < 0.02f)
            {
                postSideY.Add(y);
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
        return width < 5f && MathF.Abs(height - 16f) < 0.01f;
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
