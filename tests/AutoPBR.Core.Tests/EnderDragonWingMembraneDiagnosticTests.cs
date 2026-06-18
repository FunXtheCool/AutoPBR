using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed class EnderDragonWingMembraneDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Wing_membrane_face_uv_matches_java_texOffs_negative_origin()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        // Geometry IR preserves Java texOffs(-56, *) for dragon membranes, then shifts the
        // horizontal UP sheet by one width so Java's UP rect wraps to visible U 0..56.
        var visibleMembraneSlots = new[]
        {
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, -112, 88, 56, 0, 56),
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, -112, 88, 56, 0, 56, mirrorU: true),
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, -112, 144, 56, 0, 56),
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, -112, 144, 56, 0, 56, mirrorU: true),
        };

        var matched = 0;
        foreach (var el in merged.Elements)
        {
            if (!TryGetWingMembraneUpFace(el, out var face))
            {
                continue;
            }

            if (visibleMembraneSlots.Any(expected => face.Uv!.SequenceEqual(expected)))
            {
                matched++;
            }
        }

        Assert.True(matched >= 4, $"expected wing membrane sheets on visible negative-U atlas slots, matched {matched}");
    }

    [Fact]
    public void Wing_membrane_elements_have_nonzero_horizontal_extent_and_skin_uv_rect()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var sheetExtents = new List<(float spanX, float spanZ, float[] uv)>();
        foreach (var el in merged.Elements)
        {
            if (!TryGetWingMembraneUpFace(el, out var face))
            {
                continue;
            }

            var spanX = el.To[0] - el.From[0];
            var spanZ = el.To[2] - el.From[2];
            sheetExtents.Add((spanX, spanZ, face.Uv!));
        }

        Assert.True(sheetExtents.Count >= 4, $"expected >=4 wing membrane sheets, got {sheetExtents.Count}");
        foreach (var (spanX, spanZ, uv) in sheetExtents)
        {
            Assert.True(spanX > 0f && spanZ > 0f, "wing sheet must span XZ");
            // Dragon negative-U membranes must land on the visible artwork at U 0-56, not the transparent 56-112 gap.
            var u0 = uv[0];
            var u1 = uv[2];
            var v0 = uv[1];
            var v1 = uv[3];
            Assert.InRange(MathF.Min(u0, u1), 0f, 1f);
            Assert.InRange(MathF.Max(u0, u1), 55f, 57f);
            Assert.InRange(MathF.Min(v0, v1), 88f, 144f);
            Assert.InRange(MathF.Max(v0, v1), 144f, 200f);
        }
    }

    [Fact]
    public void Wing_membrane_mirror_flags_match_left_and_right_wing_javap_construction()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var mirrored = 0;
        var unmirrored = 0;
        foreach (var el in merged.Elements)
        {
            if (!TryGetWingMembraneUpFace(el, out _))
            {
                continue;
            }

            if (el.MirrorCuboidUv)
            {
                mirrored++;
            }
            else
            {
                unmirrored++;
            }
        }

        Assert.Equal(2, mirrored);
        Assert.Equal(2, unmirrored);
    }

    [Fact]
    public void Wing_membrane_baked_uv_stays_inside_unit_square()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (256, 256);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var wingVerts = 0;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (!IsVisibleDragonMembraneUv(u, v))
            {
                continue;
            }

            wingVerts++;
            Assert.InRange(u, 0f, 1f);
            Assert.InRange(v, 0f, 1f);
        }

        Assert.True(wingVerts >= 12, $"expected wing membrane verts, got {wingVerts}");
    }

    [Fact]
    public void Wing_membrane_baked_uv_samples_dragon_skin_region_not_wrapped_alias()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (256, 256);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sumU = 0f;
        var count = 0;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (!IsVisibleDragonMembraneUv(u, v))
            {
                continue;
            }

            sumU += u;
            count++;
        }

        Assert.True(count >= 12, $"expected wing membrane verts, got {count}");
        var meanU = sumU / count;
        Assert.InRange(meanU, 0.05f, 0.17f);
    }

    [Fact]
    public void Wing_membrane_baked_quads_span_preview_space_not_a_degenerate_point()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (256, 256);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var maxSpan = 0f;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (!IsVisibleDragonMembraneUv(u, v))
            {
                continue;
            }

            for (var j = i + stride; j < verts.Length; j += stride)
            {
                var u2 = verts[j + 6];
                var v2 = verts[j + 7];
                if (!IsVisibleDragonMembraneUv(u2, v2))
                {
                    continue;
                }

                var dx = verts[i] - verts[j];
                var dy = verts[i + 1] - verts[j + 1];
                var dz = verts[i + 2] - verts[j + 2];
                maxSpan = MathF.Max(maxSpan, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSpan > 0.5f, $"wing membrane verts should span preview space, max pairwise dist={maxSpan:F4}");
    }

    [Fact]
    public void Wing_membrane_sheets_are_double_sided_and_draw_in_base_layer_after_bone()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var sheets = 0;
        foreach (var el in merged.Elements)
        {
            if (!TryGetWingMembraneUpFace(el, out var upFace))
            {
                continue;
            }

            sheets++;
            Assert.Equal(PreviewDepthLayerKind.Base, el.DepthLayerKind);
            Assert.True(el.Faces.ContainsKey("down"));
            Assert.Equal(upFace.Uv, el.Faces["down"].Uv);
        }

        Assert.True(sheets >= 4, $"expected >=4 wing membrane sheets, got {sheets}");
    }

    [Fact]
    public void Wing_membrane_elements_stay_on_base_layer_not_cutout_overlay()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var sheets = 0;
        foreach (var el in merged.Elements)
        {
            if (!TryGetWingMembraneUpFace(el, out _))
            {
                continue;
            }

            sheets++;
            Assert.Equal(PreviewDepthLayerKind.Base, el.DepthLayerKind);
        }

        Assert.True(sheets >= 4, $"expected >=4 wing membrane sheets on Base, got {sheets}");
    }

    [Fact]
    public void Wing_membrane_baked_mesh_includes_down_face_quads()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/enderdragon/dragon.png", Profile26, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (256, 256);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out var indices, out var batches));

        var membraneVerts = 0;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (!IsVisibleDragonMembraneUv(u, v))
            {
                continue;
            }

            membraneVerts++;
        }

        Assert.True(membraneVerts >= 32, $"expected double-sided wing membrane verts, got {membraneVerts}");
    }

    private static bool IsVisibleDragonMembraneUv(float u, float v) =>
        u >= 0f &&
        u <= (57f / 256f) &&
        v >= (88f / 256f) &&
        v <= (200f / 256f);

    private static bool TryGetWingMembraneUpFace(ModelElement el, out ModelFace face)
    {
        face = null!;
        if (!el.Faces.TryGetValue("up", out var up) || up.Uv is not { Length: >= 4 })
        {
            return false;
        }

        var spanX = el.To[0] - el.From[0];
        var spanZ = el.To[2] - el.From[2];
        if (spanX < 40f || spanZ < 40f)
        {
            return false;
        }

        face = up;
        return true;
    }
}
