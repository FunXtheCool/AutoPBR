namespace AutoPBR.Core.Tests;

public sealed class EntityMembraneUvDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Camel_tail_north_south_faces_use_uvSpan_layout_not_full_box()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/camel/camel.png", Profile26, 0f, 0f, out var merged, out _));

        var javaNorth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.North, 122, 0, 3, 14, 0);
        var javaSouth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.South, 122, 0, 3, 14, 0);

        ModelElement? tail = null;
        foreach (var el in merged.Elements)
        {
            if (el.Faces.ContainsKey("north") && el.Faces.ContainsKey("south") &&
                !el.Faces.ContainsKey("east") &&
                Math.Abs(el.To[1] - el.From[1] - 14f) < 0.01f)
            {
                tail = el;
                break;
            }
        }

        Assert.NotNull(tail);
        var north = tail.Faces["north"].Uv!;
        var south = tail.Faces["south"].Uv!;

        // North/south span layout (not full-box +d padding): north [122,0,125,14], south [127,0,130,14].
        Assert.Equal(javaNorth, north);
        Assert.Equal(javaSouth, south);
    }

    [Fact]
    public void EnderDragon_wing_membrane_baked_uv_in_skin_rect_and_emits_indices()
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

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out var indices, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var wingVerts = 0;
        var maxU = 0f;
        var minU = 1f;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (v < (88f / 256f) || v > (200f / 256f))
            {
                continue;
            }

            if (u < 0f || u > (57f / 256f))
            {
                continue;
            }

            wingVerts++;
            maxU = MathF.Max(maxU, u);
            minU = MathF.Min(minU, u);
        }

        Assert.True(wingVerts >= 12, $"wing membrane verts={wingVerts}");
        Assert.True(indices.Length >= 18, $"wing membrane indices={indices.Length}");
        Assert.True(maxU <= (57f / 256f), $"max wing U {maxU:F4}");
        Assert.True(minU <= (1f / 256f), $"min wing U {minU:F4}");
    }

    [Fact]
    public void Camel_tail_baked_south_face_uv_clamps_to_atlas_edge_not_wraps()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/camel/camel.png", Profile26, 0f, 0f, out var merged, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (128, 128);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var southVerts = 0;
        var minU = 1f;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (u < 0.9f || v > 0.2f)
            {
                continue;
            }

            southVerts++;
            minU = MathF.Min(minU, u);
        }

        Assert.True(southVerts >= 4, $"expected south tail verts, got {southVerts}");
        Assert.True(minU > 0.9f, $"south tail U should stay on right edge, min {minU:F4}");
    }
}
