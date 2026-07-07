using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Java <c>ModelPart.Cube</c> UV / face-plane parity for entity cuboid baking.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class EntityCuboidUvRoutingAuditTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Theory]
    [InlineData("down", "down")]
    [InlineData("up", "up")]
    [InlineData("north", "north")]
    [InlineData("west", "west")]
    [InlineData("east", "east")]
    public void Baseline_routes_face_dictionary_key_to_matching_geometry_plane(string faceKey, string expectedPlane)
    {
        var policy = PreviewUvBakePolicy.EntityCuboidBaseline;
        Assert.Equal(expectedPlane, MinecraftModelBaker.ApplyFaceSemanticRouting(faceKey, in policy));
    }

    [Fact]
    public void Creeper_leg_source_uv_slots_match_java_direction_at_texOffs_0_16()
    {
        Assert.True(TryFindCreeperLegFaceUv(out var upUv, out var downUv));

        var javaDown = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 16, 4, 6, 4);
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 16, 4, 6, 4);

        Assert.Equal(javaDown, downUv);
        Assert.Equal(javaUp, upUv);
    }

    [Fact]
    public void Creeper_leg_face_keys_align_with_java_direction_names()
    {
        Assert.True(TryFindCreeperLegFaceUv(out _, out _));
        Assert.Equal("down", EntityCuboidJavaUvConvention.TemplateSlotName(EntityCuboidJavaUvConvention.JavaDirection.Down));
        Assert.Equal("up", EntityCuboidJavaUvConvention.TemplateSlotName(EntityCuboidJavaUvConvention.JavaDirection.Up));
    }

    [Fact]
    public void Java_up_uv_rect_preserves_modelpart_reversed_v_arguments()
    {
        var javaDown = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 16, 4, 6, 4);
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 16, 4, 6, 4);

        Assert.Equal(new float[] { 4, 16, 8, 20 }, javaDown);
        Assert.Equal(new float[] { 8, 20, 12, 16 }, javaUp);
    }

    [Theory]
    [InlineData("down", 0f, 16f)]
    [InlineData("up", 16f, 16f)]
    public void TryGetFacePlaneRaw_matches_java_cube_horizontal_planes(string faceName, float expectedY, float unused)
    {
        _ = unused;
        const float fx = 0, fy = 0, fz = 0, tx = 16, ty = 16, tz = 16;
        Assert.True(MinecraftModelBaker.TryGetFaceCornerBoundsForAudit(faceName, fx, fy, fz, tx, ty, tz, out var minY, out var maxY, out _));
        Assert.Equal(expectedY, minY, 3);
        Assert.Equal(expectedY, maxY, 3);
    }

    [Fact]
    public void Full_height_cuboid_up_face_stays_on_top_plane()
    {
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 16, 16, 16, 16);
        var model = BuildUpDownOnlyCube(upSlot: javaUp, downSlot: [0, 0, 16, 16]);
        Assert.True(Bake(model, 64, 64, out var verts));

        var topPlane = AverageUvOnHorizontalPlane(verts, yTexel: 16f, above: true);
        Assert.InRange(topPlane.X, javaUp[0] / 64f, javaUp[2] / 64f);
    }

    [Fact]
    public void Baseline_maps_java_direction_down_uv_to_bottom_plane()
    {
        var javaDown = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 16, 16, 16, 16);
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 16, 16, 16, 16);

        var model = BuildUpDownOnlyCube(upSlot: javaUp, downSlot: javaDown);
        Assert.True(Bake(model, 64, 64, out var verts));

        var bottomPlane = AverageUvOnHorizontalPlane(verts, yTexel: 0f, above: false);
        var topPlane = AverageUvOnHorizontalPlane(verts, yTexel: 16f, above: true);

        Assert.InRange(bottomPlane.X, javaDown[0] / 64f, javaDown[2] / 64f);
        Assert.InRange(topPlane.X, javaUp[0] / 64f, javaUp[2] / 64f);
    }

    [Fact]
    public void Java_corner_order_maps_up_face_uv_to_modelpart_polygon_corners()
    {
        var model = BuildUpDownOnlyCube(upSlot: [10, 20, 30, 40], downSlot: [0, 0, 16, 16]);
        Assert.True(Bake(model, 64, 64, out var verts));

        AssertUvAtPosition(verts, x: 0.5f, y: 0.5f, z: -0.5f, expectedU: 30f / 64f, expectedV: 20f / 64f);
        AssertUvAtPosition(verts, x: -0.5f, y: 0.5f, z: -0.5f, expectedU: 10f / 64f, expectedV: 20f / 64f);
        AssertUvAtPosition(verts, x: -0.5f, y: 0.5f, z: 0.5f, expectedU: 10f / 64f, expectedV: 40f / 64f);
        AssertUvAtPosition(verts, x: 0.5f, y: 0.5f, z: 0.5f, expectedU: 30f / 64f, expectedV: 40f / 64f);
    }

    [Fact]
    public void Java_mirror_reverses_polygon_after_uv_remap_without_swapping_uv_bounds()
    {
        var model = BuildUpDownOnlyCube(upSlot: [10, 20, 30, 40], downSlot: [0, 0, 16, 16], mirrorCuboidUv: true);
        Assert.True(Bake(model, 64, 64, out var verts));

        AssertUvAtPosition(verts, x: -0.5f, y: 0.5f, z: 0.5f, expectedU: 30f / 64f, expectedV: 40f / 64f);
        AssertUvAtPosition(verts, x: 0.5f, y: 0.5f, z: 0.5f, expectedU: 10f / 64f, expectedV: 40f / 64f);
        AssertUvAtPosition(verts, x: 0.5f, y: 0.5f, z: -0.5f, expectedU: 10f / 64f, expectedV: 20f / 64f);
        AssertUvAtPosition(verts, x: -0.5f, y: 0.5f, z: -0.5f, expectedU: 30f / 64f, expectedV: 20f / 64f);
    }

    [Fact]
    public void Ler_root_transform_keeps_java_direction_west_uv_on_transformed_west_polygon()
    {
        var javaWest = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.West, 0, 0, 16, 16, 16);
        var javaEast = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.East, 0, 0, 16, 16, 16);

        var model = ApplyEntityPreviewLer(BuildEastWestOnlyCube(westSlot: javaWest, eastSlot: javaEast));
        Assert.True(Bake(model, 64, 64, out var verts));

        var transformedWestPlane = AverageUvOnVerticalPlane(verts, xTexel: 0f, positiveSide: true);
        var transformedEastPlane = AverageUvOnVerticalPlane(verts, xTexel: -16f, positiveSide: false);

        Assert.InRange(transformedWestPlane.X, javaWest[0] / 64f, javaWest[2] / 64f);
        Assert.InRange(transformedEastPlane.X, javaEast[0] / 64f, javaEast[2] / 64f);
    }

    [Fact]
    public void Baseline_maps_java_direction_west_uv_to_element_west_plane_without_ler()
    {
        var javaWest = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.West, 0, 0, 16, 16, 16);
        var javaEast = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.East, 0, 0, 16, 16, 16);

        var model = BuildEastWestOnlyCube(westSlot: javaWest, eastSlot: javaEast);

        Assert.True(Bake(model, 64, 64, out var verts));

        var westPlane = AverageUvOnVerticalPlane(verts, xTexel: 0f, positiveSide: false);
        var eastPlane = AverageUvOnVerticalPlane(verts, xTexel: 16f, positiveSide: true);

        Assert.InRange(westPlane.X, javaWest[0] / 64f, javaWest[2] / 64f);
        Assert.InRange(eastPlane.X, javaEast[0] / 64f, javaEast[2] / 64f);
    }

    [Fact]
    public void Legacy_swap_face_east_west_inverts_lateral_uv_without_ler()
    {
        var javaWest = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.West, 0, 0, 16, 16, 16);
        var javaEast = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.East, 0, 0, 16, 16, 16);

        var model = BuildEastWestOnlyCube(westSlot: javaWest, eastSlot: javaEast);
        var swapped = PreviewUvBakePolicy.EntityCuboidBaseline with { SwapFaceEastWest = true };

        Assert.True(Bake(model, 64, 64, swapped, out var verts));

        var westPlane = AverageUvOnVerticalPlane(verts, xTexel: 0f, positiveSide: false);
        var eastPlane = AverageUvOnVerticalPlane(verts, xTexel: 16f, positiveSide: true);

        Assert.InRange(westPlane.X, javaEast[0] / 64f, javaEast[2] / 64f);
        Assert.InRange(eastPlane.X, javaWest[0] / 64f, javaWest[2] / 64f);
    }

    [Fact]
    public void Legacy_swap_face_up_down_inverts_vertical_uv_placement()
    {
        var javaDown = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 16, 16, 16, 16);
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 16, 16, 16, 16);

        var model = BuildUpDownOnlyCube(upSlot: javaUp, downSlot: javaDown);
        var legacy = PreviewUvBakePolicy.EntityCuboidBaseline with { SwapFaceUpDown = true };

        Assert.True(Bake(model, 64, 64, legacy, out var verts));

        var bottomPlane = AverageUvOnHorizontalPlane(verts, yTexel: 0f, above: false);
        var topPlane = AverageUvOnHorizontalPlane(verts, yTexel: 16f, above: true);

        Assert.InRange(bottomPlane.X, javaUp[0] / 64f, javaUp[2] / 64f);
        Assert.InRange(topPlane.X, javaDown[0] / 64f, javaDown[2] / 64f);
    }

    private static MergedJavaBlockModel ApplyEntityPreviewLer(MergedJavaBlockModel model)
    {
        var elements = new List<ModelElement>(model.Elements.Count);
        foreach (var e in model.Elements)
        {
            elements.Add(new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = EntityModelRuntime.ApplyLivingEntityRendererColumnRootScale(e.LocalToParent),
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = true,
        };
    }

    private static bool TryFindCreeperLegFaceUv(out float[] upUv, out float[] downUv)
    {
        upUv = [];
        downUv = [];

        var runtime = EntityModelRuntimeFactory.Create();
        if (!runtime.TryBuildStaticMesh(
                "assets/minecraft/textures/entity/creeper/creeper.png",
                Profile26,
                0f,
                0f,
                out var merged,
                out _))
        {
            return false;
        }

        foreach (var el in merged.Elements)
        {
            var h = el.To[1] - el.From[1];
            var w = el.To[0] - el.From[0];
            var d = el.To[2] - el.From[2];
            if (Math.Abs(w - 4f) > 0.01f || Math.Abs(h - 6f) > 0.01f || Math.Abs(d - 4f) > 0.01f)
            {
                continue;
            }

            if (!el.Faces.TryGetValue("up", out var upFace) || upFace.Uv is not { Length: >= 4 })
            {
                continue;
            }

            if (!el.Faces.TryGetValue("down", out var downFace) || downFace.Uv is not { Length: >= 4 })
            {
                continue;
            }

            upUv = upFace.Uv;
            downUv = downFace.Uv;
            return true;
        }

        return false;
    }

    private static MergedJavaBlockModel BuildEastWestOnlyCube(float[] westSlot, float[] eastSlot)
    {
        const string tex = "entity/creeper/creeper";
        return new MergedJavaBlockModel
        {
            Textures = new Dictionary<string, string>(StringComparer.Ordinal) { ["skin"] = tex },
            Elements =
            [
                new ModelElement
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["west"] = new() { TextureKey = "skin", Uv = westSlot },
                        ["east"] = new() { TextureKey = "skin", Uv = eastSlot },
                    },
                },
            ],
        };
    }

    private static MergedJavaBlockModel BuildUpDownOnlyCube(float[] upSlot, float[] downSlot, bool mirrorCuboidUv = false)
    {
        const string tex = "entity/creeper/creeper";
        return new MergedJavaBlockModel
        {
            Textures = new Dictionary<string, string>(StringComparer.Ordinal) { ["skin"] = tex },
            Elements =
            [
                new ModelElement
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["up"] = new() { TextureKey = "skin", Uv = upSlot },
                        ["down"] = new() { TextureKey = "skin", Uv = downSlot },
                    },
                    MirrorCuboidUv = mirrorCuboidUv,
                },
            ],
        };
    }

    private static bool Bake(MergedJavaBlockModel model, int atlasW, int atlasH, out float[] vertices) =>
        Bake(model, atlasW, atlasH, PreviewUvBakePolicy.Resolve(model), out vertices);

    private static bool Bake(
        MergedJavaBlockModel model,
        int atlasW,
        int atlasH,
        PreviewUvBakePolicy policy,
        out float[] vertices)
    {
        vertices = [];
        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(model, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (atlasW, atlasH);
        }

        if (!MinecraftModelBaker.TryBakeWithUvPolicy(model, "minecraft", pathToIdx, texSizes, in policy, out vertices, out _, out _))
        {
            return false;
        }

        return vertices.Length > 0;
    }

    private static Vector2 AverageUvOnVerticalPlane(ReadOnlySpan<float> verts, float xTexel, bool positiveSide)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sum = Vector2.Zero;
        var count = 0;
        var xModel = xTexel / 16f - 0.5f;

        for (var i = 0; i < verts.Length; i += stride)
        {
            var x = verts[i];
            if (positiveSide ? x < xModel - 0.001f : x > xModel + 0.001f)
            {
                continue;
            }

            sum += new Vector2(verts[i + 6], verts[i + 7]);
            count++;
        }

        Assert.True(count > 0, $"no vertices on vertical plane x={xTexel}");
        return sum / count;
    }

    private static Vector2 AverageUvOnHorizontalPlane(ReadOnlySpan<float> verts, float yTexel, bool above)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sum = Vector2.Zero;
        var count = 0;
        var yModel = yTexel / 16f - 0.5f;

        for (var i = 0; i < verts.Length; i += stride)
        {
            var y = verts[i + 1];
            if (above ? y < yModel - 0.001f : y > yModel + 0.001f)
            {
                continue;
            }

            sum += new Vector2(verts[i + 6], verts[i + 7]);
            count++;
        }

        Assert.True(count > 0, $"no vertices on horizontal plane y={yTexel}");
        return sum / count;
    }

    private static void AssertUvAtPosition(
        ReadOnlySpan<float> verts,
        float x,
        float y,
        float z,
        float expectedU,
        float expectedV)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var i = 0; i < verts.Length; i += stride)
        {
            if (Math.Abs(verts[i] - x) > 0.001f ||
                Math.Abs(verts[i + 1] - y) > 0.001f ||
                Math.Abs(verts[i + 2] - z) > 0.001f)
            {
                continue;
            }

            Assert.Equal(expectedU, verts[i + 6], 4);
            Assert.Equal(expectedV, verts[i + 7], 4);
            return;
        }

        Assert.Fail($"no vertex at ({x:F3},{y:F3},{z:F3})");
    }
}
