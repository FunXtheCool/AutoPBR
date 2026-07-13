using System.Numerics;
using System.Text.Json;
using AutoPBR.Preview;
using AutoPBR.Preview.GeometryIr;

namespace AutoPBR.Core.Tests;

/// <summary>T1: Breeze multi-atlas parity-catalog geometry IR emit (body 32², wind 128², companion paths).</summary>
public sealed class GeometryIrBreezeParityEmitTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Breeze_wind_manifest_atlas_is_128()
    {
        var rule = EntityTextureParityCatalog.ResolveRule(
            "assets/minecraft/textures/entity/breeze/breeze_wind.png",
            "breeze_wind");
        Assert.NotNull(rule);
        Assert.Equal(128, rule.GeometryIrTextureWidth);
        Assert.Equal(128, rule.GeometryIrTextureHeight);
    }

    [Fact]
    public void Breeze_wind_rebake_uses_128_logical_atlas_not_shard_primary_32()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze_wind.png";
        var provenance = new PreviewMeshProvenance(
            PreviewMeshDriverKind.RuntimeGeometryIrJson,
            "net.minecraft.client.model.monster.breeze.BreezeModel");
        var size = EntityGeometryIrTextureAtlas.ResolveForBake(path, 128, 128, provenance, Profile26);
        Assert.Equal((128, 128), size);
    }

    [Fact]
    public void Breeze_wind_baked_uv_fingerprint_matches_128_atlas_not_32()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze_wind.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var merged, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i], 128, 128, provenance, Profile26);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var logicalVerts, out _, out _));

        var wrongMerged = ZeroElementBakeAtlases(merged);
        for (var i = 0; i < ordered.Count; i++)
        {
            texSizes[ordered[i]] = (32, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(wrongMerged, "minecraft", pathToIdx, texSizes, out var wrongVerts, out _, out _));
        Assert.NotEqual(ComputeUvFingerprint(logicalVerts), ComputeUvFingerprint(wrongVerts));
    }

    [Fact]
    public void Breeze_wind_geometry_ir_emit_maps_high_u_on_128_atlas()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze_wind.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(TryFindElementWithSize(mesh, 18f, 8f, 18f, out var outerShell));
        Assert.True(outerShell.Faces.TryGetValue("north", out var north) && north.Uv is { Length: >= 4 });
        Assert.True(north.Uv![2] > 32f, "wind_top outer shell north U2 should stay on 128² sheet (not 32² wrap)");

        Assert.True(TryFindElementWithSize(mesh, 5f, 7f, 5f, out var bottomTier));
        Assert.True(bottomTier.Faces.TryGetValue("north", out var bottomNorth) && bottomNorth.Uv is { Length: >= 4 });
        Assert.True(bottomNorth.Uv![0] >= 1f && bottomNorth.Uv[0] < 32f,
            "wind_bottom tier should keep low-U layout from texOffs (1,83) on 128 atlas");
    }

    [Fact]
    public void Breeze_main_geometry_ir_composite_includes_wind_and_companion_textures()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(HasElementSize(mesh, 8f, 8f, 8f));
        Assert.True(HasElementSize(mesh, 18f, 8f, 18f));
        Assert.True(mesh.Textures.ContainsKey("wind"));
        Assert.True(mesh.Textures.ContainsKey("eyes"));
        Assert.Contains("breeze_wind", mesh.Textures["wind"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("breeze_eyes", mesh.Textures["eyes"], StringComparison.OrdinalIgnoreCase);

        Assert.Contains(mesh.Elements, el =>
            el.Faces.Values.Any(f =>
                string.Equals(f.TextureKey, "#wind", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Breeze_geometry_ir_bind_pose_has_expected_key_part_extents()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var ir, out var provenance, applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(HasElementSize(ir, 18f, 8f, 18f));
        Assert.True(HasElementSize(ir, 8f, 8f, 8f));
    }

    [Fact]
    public void Breeze_main_bind_pose_wind_shell_overlaps_head_vertical_extent()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0f, animationTimeSeconds: 0f,
            out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        Assert.True(TryFindElementWithSize(mesh, 18f, 8f, 18f, out var windShell));
        Assert.True(TryFindElementWithSize(mesh, 8f, 8f, 8f, out var headBody));

        var windY = (windShell.From[1] + windShell.To[1]) * 0.5f;
        var headY = (headBody.From[1] + headBody.To[1]) * 0.5f;
        Assert.InRange(MathF.Abs(windY - headY), 0f, 12f);
    }

    [Fact]
    public void Breeze_main_eyes_overlay_aligns_with_head_not_body_origin()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out _));

        var headSkin = mesh.Elements.Where(el =>
            el.Faces.Values.Any(f =>
                string.Equals(f.TextureKey, "#skin", StringComparison.OrdinalIgnoreCase) &&
                MathF.Abs(el.To[1] - el.From[1] - 3f) < 0.5f)).ToList();
        var eyesOverlay = mesh.Elements.Where(el =>
            el.Faces.Values.Any(f =>
                string.Equals(f.TextureKey, "#eyes", StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.NotEmpty(headSkin);
        Assert.NotEmpty(eyesOverlay);

        var headY = headSkin[0].From[1];
        var eyesY = eyesOverlay[0].From[1];
        Assert.InRange(MathF.Abs(eyesY - headY), 0f, 1.5f);
    }

    [Fact]
    public void Breeze_main_skin_emit_pixel_uvs_remain_on_32_atlas()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        var skinMaxPixelU = 0f;
        var skinCount = 0;
        var windMaxPixelU = 0f;
        var windCount = 0;
        foreach (var el in mesh.Elements)
        {
            if (!TryGetElementPrimaryTextureKey(el, out var key))
            {
                continue;
            }

            var maxU = MaxFacePixelU(el);
            if (string.Equals(key, "#skin", StringComparison.OrdinalIgnoreCase))
            {
                skinCount++;
                skinMaxPixelU = MathF.Max(skinMaxPixelU, maxU);
            }
            else if (string.Equals(key, "#wind", StringComparison.OrdinalIgnoreCase))
            {
                windCount++;
                windMaxPixelU = MathF.Max(windMaxPixelU, maxU);
            }
        }

        Assert.True(skinCount >= 3, $"expected head + rods on #skin (count={skinCount})");
        Assert.True(windCount >= 3, $"expected wind tiers on #wind (count={windCount})");
        Assert.True(skinMaxPixelU <= 36f, $"skin texels must stay on 32² sheet (maxU={skinMaxPixelU:F1})");
        Assert.True(windMaxPixelU > 40f, $"wind texels must use 128² sheet (maxU={windMaxPixelU:F1})");
    }

    [Fact]
    public void Breeze_main_rebake_resolve_for_bake_uses_per_path_atlas_not_shard_only()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var merged, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        foreach (var texPath in ordered)
        {
            var physicalW = texPath.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase) ? 128 : 32;
            var physicalH = physicalW;
            var bake = EntityGeometryIrTextureAtlas.ResolveForBake(
                texPath, physicalW, physicalH, provenance, Profile26);
            if (texPath.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal((128, 128), bake);
            }
            else if (texPath.Contains("breeze.png", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal((32, 32), bake);
            }
            else
            {
                Assert.Equal((32, 32), bake);
            }
        }
    }

    [Fact]
    public void Breeze_main_skin_baked_uvs_use_32_atlas_not_wind_128()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var merged, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i], 32, 32, provenance, Profile26);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var logicalVerts, out _, out var batches));

        var skinIdx = ordered.FindIndex(p => p.Contains("breeze.png", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase));
        Assert.True(skinIdx >= 0);

        var skinBatch = batches.FirstOrDefault(b => b.MaterialIndex == skinIdx);
        Assert.True(skinBatch.IndexCount > 0, "expected #skin draw batch on main breeze composite");

        Assert.True(TryReadBatchUvSpan(logicalVerts, skinBatch, out var minU, out var maxU, out var minV, out var maxV));
        Assert.True(maxU > 0.15f, $"skin UV span too narrow for 32² head/rod layout (maxU={maxU:F3})");
        Assert.True(maxV > 0.15f, $"skin UV span too narrow for 32² head/rod layout (maxV={maxV:F3})");

        for (var i = 0; i < ordered.Count; i++)
        {
            texSizes[ordered[i]] = (128, 128);
        }

        Assert.True(MinecraftModelBaker.TryBake(ZeroElementBakeAtlases(merged), "minecraft", pathToIdx, texSizes, out var wrongVerts, out _, out _));
        Assert.NotEqual(ComputeUvFingerprint(logicalVerts), ComputeUvFingerprint(wrongVerts));
    }

    [Fact]
    public void Breeze_part_tree_repair_stamps_missing_wind_layer_atlas_tags()
    {
        const string jvm = "net.minecraft.client.model.monster.breeze.BreezeModel";
        var shardPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "geometry",
            "26.1.2",
            $"{jvm}.json");
        Assert.True(File.Exists(shardPath), shardPath);
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(TryFindCuboid(repaired, "wind_bottom", out var windCuboid));
        Assert.Equal("#wind", windCuboid.GetProperty("textureKey").GetString());
        Assert.Equal(128, windCuboid.GetProperty("textureWidth").GetInt32());
        Assert.True(TryFindCuboid(repaired, "head", out var headCuboid));
        Assert.Equal("#skin", headCuboid.GetProperty("textureKey").GetString());
        Assert.Equal(32, headCuboid.GetProperty("textureWidth").GetInt32());
    }

    [Fact]
    public void Breeze_main_skin_baked_uvs_survive_hd_primary_physical_texture()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var merged, out var provenance, applyGeometryIrSetupAnimMotion: false));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i], 128, 128, provenance, Profile26);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out var batches));

        var skinIdx = ordered.FindIndex(p => p.Contains("breeze.png", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase));
        Assert.True(skinIdx >= 0);
        var skinBatch = batches.FirstOrDefault(b => b.MaterialIndex == skinIdx);
        Assert.True(skinBatch.IndexCount > 0);
        Assert.True(TryReadBatchUvSpan(verts, skinBatch, out _, out var maxU, out _, out var maxV));
        Assert.True(maxU > 0.15f, $"skin batch UV span too narrow with HD primary (maxU={maxU:F3})");
        Assert.True(maxV > 0.15f, $"skin batch UV span too narrow with HD primary (maxV={maxV:F3})");
    }

    [Fact]
    public void Breeze_main_elements_stamp_per_cuboid_bake_atlas_dimensions()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        var skinElements = mesh.Elements.Where(el =>
            el.Faces.Values.Any(f => string.Equals(f.TextureKey, "#skin", StringComparison.OrdinalIgnoreCase))).ToList();
        var windElements = mesh.Elements.Where(el =>
            el.Faces.Values.Any(f => string.Equals(f.TextureKey, "#wind", StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.NotEmpty(skinElements);
        Assert.NotEmpty(windElements);
        Assert.All(skinElements.Where(el => el.DepthLayerKind == PreviewDepthLayerKind.Base),
            el => Assert.Equal(32, el.BakeAtlasWidth));
        Assert.All(windElements, el => Assert.Equal(128, el.BakeAtlasWidth));
    }

    [Fact]
    public void Breeze_part_tree_repair_nests_rods_under_rods_anchor()
    {
        const string jvm = "net.minecraft.client.model.monster.breeze.BreezeModel";
        var shardPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "geometry",
            "26.1.2",
            $"{jvm}.json");
        Assert.True(File.Exists(shardPath), shardPath);
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(TryFindPart(repaired, "rods", out var rodsPart));
        Assert.True(TryFindChildPart(rodsPart, "rod_1", out _));
        Assert.True(TryFindChildPart(rodsPart, "rod_2", out _));
        Assert.True(TryFindChildPart(rodsPart, "rod_3", out _));
    }

    [Fact]
    public void Breeze_main_bind_pose_rods_cluster_near_head_not_at_floor()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0f, animationTimeSeconds: 0f,
            out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        Assert.True(TryFindElementWithSize(mesh, 8f, 8f, 8f, out var headBody));
        var rodElements = mesh.Elements.Where(el =>
        {
            var w = MathF.Abs(el.To[0] - el.From[0]);
            var h = MathF.Abs(el.To[1] - el.From[1]);
            var d = MathF.Abs(el.To[2] - el.From[2]);
            return MathF.Abs(w - 2f) <= 0.6f && MathF.Abs(h - 8f) <= 0.6f && MathF.Abs(d - 2f) <= 0.6f;
        }).ToList();
        Assert.Equal(3, rodElements.Count);

        static float CentroidY(ModelElement el) => (el.From[1] + el.To[1]) * 0.5f;
        static Vector3 WorldCorner(ModelElement el) =>
            new(el.LocalToParent.M41, el.LocalToParent.M42, el.LocalToParent.M43);

        var headWorldY = WorldCorner(headBody).Y + CentroidY(headBody);
        var rodWorldY = rodElements.Average(el => WorldCorner(el).Y + CentroidY(el));
        Assert.InRange(MathF.Abs(rodWorldY - headWorldY), 0f, 10f);
        Assert.True(rodWorldY > -4f, $"rods should stay attached to torso (rodWorldY={rodWorldY:F2})");
    }

    [Fact]
    public void Breeze_eyes_geometry_ir_path_emits_eyes_part_only()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze_eyes.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var mesh, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(HasElementSize(mesh, 10f, 3f, 4f));
        Assert.False(HasElementSize(mesh, 18f, 8f, 18f));
        Assert.False(HasElementSize(mesh, 2f, 8f, 2f));
    }

    private static MergedJavaBlockModel ZeroElementBakeAtlases(MergedJavaBlockModel source)
    {
        var elements = new ModelElement[source.Elements.Count];
        for (var i = 0; i < source.Elements.Count; i++)
        {
            var e = source.Elements[i];
            elements[i] = new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = e.LocalToParent,
                DepthLayerKind = e.DepthLayerKind,
                LayerOrdinal = e.LayerOrdinal,
                CastsShadow = e.CastsShadow,
                ShellInflateTexels = e.ShellInflateTexels,
                EnableParallax = e.EnableParallax,
                MirrorCuboidUv = e.MirrorCuboidUv,
                BakeAtlasWidth = 0,
                BakeAtlasHeight = 0,
            };
        }

        return new MergedJavaBlockModel
        {
            Elements = elements.ToList(),
            Textures = source.Textures,
            UsesLivingEntityRendererColumnYFlip = source.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static bool HasElementSize(MergedJavaBlockModel model, float w, float h, float d, float tol = 0.6f)
    {
        foreach (var el in model.Elements)
        {
            var ew = MathF.Abs(el.To[0] - el.From[0]);
            var eh = MathF.Abs(el.To[1] - el.From[1]);
            var ed = MathF.Abs(el.To[2] - el.From[2]);
            if (MathF.Abs(ew - w) <= tol && MathF.Abs(eh - h) <= tol && MathF.Abs(ed - d) <= tol)
            {
                return true;
            }
        }

        return false;
    }

    private static float MinCornerDistByExtents(
        MergedJavaBlockModel a,
        MergedJavaBlockModel b,
        float w,
        float h,
        float d,
        float tol = 0.6f)
    {
        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var best = float.MaxValue;
        foreach (var ae in a.Elements)
        {
            var ew = MathF.Abs(ae.To[0] - ae.From[0]);
            var eh = MathF.Abs(ae.To[1] - ae.From[1]);
            var ed = MathF.Abs(ae.To[2] - ae.From[2]);
            if (MathF.Abs(ew - w) > tol || MathF.Abs(eh - h) > tol || MathF.Abs(ed - d) > tol)
            {
                continue;
            }

            foreach (var be in b.Elements)
            {
                var bw = MathF.Abs(be.To[0] - be.From[0]);
                var bh = MathF.Abs(be.To[1] - be.From[1]);
                var bd = MathF.Abs(be.To[2] - be.From[2]);
                if (MathF.Abs(bw - w) > tol || MathF.Abs(bh - h) > tol || MathF.Abs(bd - d) > tol)
                {
                    continue;
                }

                best = MathF.Min(best, Vector3.Distance(Corner(ae.LocalToParent), Corner(be.LocalToParent)));
            }
        }

        return best;
    }

    private static bool TryFindElementWithSize(
        MergedJavaBlockModel model,
        float w,
        float h,
        float d,
        out ModelElement element,
        float tol = 0.6f)
    {
        foreach (var el in model.Elements)
        {
            var ew = MathF.Abs(el.To[0] - el.From[0]);
            var eh = MathF.Abs(el.To[1] - el.From[1]);
            var ed = MathF.Abs(el.To[2] - el.From[2]);
            if (MathF.Abs(ew - w) <= tol && MathF.Abs(eh - h) <= tol && MathF.Abs(ed - d) <= tol)
            {
                element = el;
                return true;
            }
        }

        element = null!;
        return false;
    }

    private static float MaxFacePixelU(ModelElement el)
    {
        var maxU = 0f;
        foreach (var face in el.Faces.Values)
        {
            if (face.Uv is not { Length: >= 4 } uv)
            {
                continue;
            }

            maxU = MathF.Max(maxU, MathF.Max(uv[0], uv[2]));
        }

        return maxU;
    }

    private static bool TryGetElementPrimaryTextureKey(ModelElement el, out string key)
    {
        key = "";
        foreach (var face in el.Faces.Values)
        {
            if (!string.IsNullOrEmpty(face.TextureKey))
            {
                key = face.TextureKey;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPart(JsonElement geometryRoot, string partId, out JsonElement part)
    {
        part = default;
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryFindPartRecursive(root, partId, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPartRecursive(JsonElement node, string partId, out JsonElement part)
    {
        part = default;
        if (node.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
        {
            part = node;
            return true;
        }

        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (TryFindPartRecursive(child, partId, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindChildPart(JsonElement parent, string childId, out JsonElement child)
    {
        child = default;
        if (!parent.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var node in children.EnumerateArray())
        {
            if (node.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), childId, StringComparison.Ordinal))
            {
                child = node;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindCuboid(JsonElement geometryRoot, string partId, out JsonElement cuboid)
    {
        cuboid = default;
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryFindCuboidRecursive(root, partId, out cuboid))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindCuboidRecursive(JsonElement part, string partId, out JsonElement cuboid)
    {
        cuboid = default;
        if (part.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal) &&
            part.TryGetProperty("cuboids", out var cuboids) &&
            cuboids.ValueKind == JsonValueKind.Array &&
            cuboids.GetArrayLength() > 0)
        {
            cuboid = cuboids[0];
            return true;
        }

        if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (TryFindCuboidRecursive(child, partId, out cuboid))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadBatchUvSpan(
        ReadOnlySpan<float> verts,
        PreviewDrawBatch batch,
        out float minU,
        out float maxU,
        out float minV,
        out float maxV)
    {
        minU = minV = float.MaxValue;
        maxU = maxV = float.MinValue;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var end = batch.FirstIndex + batch.IndexCount;
        if (end > verts.Length / stride)
        {
            return false;
        }

        for (var vi = batch.FirstIndex; vi < end; vi++)
        {
            var baseIdx = (int)vi * stride;
            var u = verts[baseIdx + 6];
            var v = verts[baseIdx + 7];
            minU = MathF.Min(minU, u);
            maxU = MathF.Max(maxU, u);
            minV = MathF.Min(minV, v);
            maxV = MathF.Max(maxV, v);
        }

        return minU <= maxU && minV <= maxV;
    }

    private static ulong ComputeUvFingerprint(ReadOnlySpan<float> verts)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            const int stride = MinecraftModelBaker.FloatsPerVertex;
            for (var i = 6; i < verts.Length; i += stride)
            {
                hash ^= BitConverter.SingleToUInt32Bits(verts[i]);
                hash *= 1099511628211UL;
                hash ^= BitConverter.SingleToUInt32Bits(verts[i + 1]);
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
