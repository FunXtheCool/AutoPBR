using System.Numerics;
using AutoPBR.Preview;

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

        for (var i = 0; i < ordered.Count; i++)
        {
            texSizes[ordered[i]] = (32, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var wrongVerts, out _, out _));
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
