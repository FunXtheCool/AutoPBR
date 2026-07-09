using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
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

}
