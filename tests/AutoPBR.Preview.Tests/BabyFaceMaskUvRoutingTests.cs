using System.Numerics;
using System.Text.Json;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// faceMask sheet cuboids must keep Java cube north/south UV when preview-only thicken runs.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class BabyFaceMaskUvRoutingTests
{
    private const string AdultAxolotlJvm = "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel";
    private const string BabyAxolotlJvm = "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel";
    private const string BabyChickenJvm = "net.minecraft.client.model.animal.chicken.BabyChickenModel";
    private const string BabyCamelJvm = "net.minecraft.client.model.animal.camel.BabyCamelModel";

    [Fact]
    public void Axolotl_gill_sheet_keeps_java_cube_north_south_uv_with_preview_thicken()
    {
        const string cuboid = """
            {
              "from": [-4, -3, 0],
              "to": [4, 0, 0],
              "uvOrigin": [3, 37],
              "faceMask": ["north", "south"],
              "uvSpan": [8, 3],
              "inflate": 0.001
            }
            """;

        AssertFaceUv(
            cuboid,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 64,
                AtlasHeight = 64,
                OfficialJvmName = AdultAxolotlJvm,
                PreviewDegenerateAxisThickness = 1f,
            },
            "top_gills",
            "north",
            [3, 37, 11, 40]);
        AssertFaceUv(
            cuboid,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 64,
                AtlasHeight = 64,
                OfficialJvmName = AdultAxolotlJvm,
                PreviewDegenerateAxisThickness = 1f,
            },
            "top_gills",
            "south",
            [11, 37, 19, 40]);
    }

    [Fact]
    public void Baby_chicken_wing_north_south_sheet_keeps_java_cube_uv_with_preview_thicken()
    {
        const string cuboid = """
            {
              "from": [-0.5, 0, 0],
              "to": [0.5, 2, 0],
              "uvOrigin": [2, 2],
              "faceMask": ["north", "south"],
              "uvSpan": [1, 2]
            }
            """;

        AssertFaceUv(
            cuboid,
            ChickenWingOptions(),
            "left_wing",
            "north",
            [2, 2, 3, 4]);
        AssertFaceUv(
            cuboid,
            ChickenWingOptions(),
            "left_wing",
            "south",
            [3, 2, 4, 4]);
    }

    [Fact]
    public void Baby_chicken_wing_up_down_sheet_keeps_java_membrane_uv_with_preview_thicken()
    {
        const string cuboid = """
            {
              "from": [-0.5, 2, -1],
              "to": [0.5, 2, 0],
              "uvOrigin": [0, 1],
              "faceMask": ["up", "down"],
              "uvSpan": [1, 0, 1]
            }
            """;

        var up = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 1, 1, 0, 1);
        var down = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 1, 1, 0, 1);

        AssertFaceUv(cuboid, ChickenWingOptions(), "left_wing", "up", up);
        AssertFaceUv(cuboid, ChickenWingOptions(), "left_wing", "down", down);
    }

    [Fact]
    public void Baby_camel_neck_sheet_keeps_java_cube_north_south_uv_with_preview_thicken()
    {
        const string cuboid = """
            {
              "from": [-1.5, -0.5, 0],
              "to": [1.5, 8.5, 0],
              "uvOrigin": [50, 38],
              "faceMask": ["north", "south"]
            }
            """;

        AssertFaceUv(
            cuboid,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 128,
                AtlasHeight = 128,
                OfficialJvmName = BabyCamelJvm,
                PreviewDegenerateAxisThickness = 0.08f,
            },
            "neck",
            "north",
            [50, 38, 53, 47]);
        AssertFaceUv(
            cuboid,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 128,
                AtlasHeight = 128,
                OfficialJvmName = BabyCamelJvm,
                PreviewDegenerateAxisThickness = 0.08f,
            },
            "neck",
            "south",
            [53, 38, 56, 47]);
    }

    [Fact]
    public void Chicken_temperate_baby_rebake_uses_shard_16x16_atlas_not_manifest_placeholder()
    {
        const string texturePath = "assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out var provenance));

        var size = EntityGeometryIrTextureAtlas.ResolveForBake(texturePath, 64, 64, provenance, Profile26);
        Assert.Equal((16, 16), (size.Width, size.Height));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i], 64, 64, provenance, Profile26);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
        Assert.True(verts.Length > 0);
        var maxV = 0f;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var i = 7; i < verts.Length; i += stride)
        {
            maxV = MathF.Max(maxV, verts[i]);
        }

        Assert.True(maxV <= 1.01f, $"normalized V {maxV:F3} exceeds 16x16 shard atlas");
    }

    [Fact]
    public void Axolotl_blue_baby_rebake_uses_shard_32x32_atlas_not_manifest_placeholder()
    {
        const string texturePath = "assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out _, out var provenance));

        var size = EntityGeometryIrTextureAtlas.ResolveForBake(texturePath, 64, 64, provenance, Profile26);
        Assert.Equal((32, 32), (size.Width, size.Height));
    }

    [Fact]
    public void Cow_temperate_baby_rebake_shard_and_manifest_agree_at_64x64()
    {
        const string texturePath = "assets/minecraft/textures/entity/cow/cow_temperate_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out _, out var provenance));

        var size = EntityGeometryIrTextureAtlas.ResolveForBake(texturePath, 64, 64, provenance, Profile26);
        Assert.Equal((64, 64), (size.Width, size.Height));
    }

    [Fact]
    public void Baby_axolotl_tail_fin_and_leg_sheets_use_thin_preview_thickness_not_doubled_planes()
    {
        const string tailCuboid = """
            {
              "from": [0, -1.5, -1],
              "to": [0, 1.5, 7],
              "uvOrigin": [10, 9],
              "faceMask": ["east", "west"],
              "uvSpan": [0, 3, 8]
            }
            """;
        const string legCuboid = """
            {
              "from": [0, 0, -0.5],
              "to": [3, 0, 0.5],
              "uvOrigin": [20, 13],
              "faceMask": ["up", "down"],
              "uvSpan": [3, 0, 0]
            }
            """;

        var options = new GeometryIrMeshEmitOptions
        {
            Fidelity = GeometryIrEmitFidelity.Parity,
            AtlasWidth = 32,
            AtlasHeight = 32,
            OfficialJvmName = BabyAxolotlJvm,
            PreviewDegenerateAxisThickness = 0.06f,
        };

        AssertLocalAxisSpan(tailCuboid, options, axis: 0, maxSpan: 0.15f, "tail X");
        AssertLocalAxisSpan(legCuboid, options, axis: 1, maxSpan: 0.15f, "leg Y");
    }

    [Fact]
    public void Bat_runtime_wing_sheets_use_thin_preview_thickness()
    {
        const string texturePath = "assets/minecraft/textures/entity/bat/bat.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out _));

        var sheets = merged.Elements
            .Where(e => HasOnlyFaces(e, "north", "south") &&
                        AxisSpan(e, axis: 0) >= 1.5f &&
                        AxisSpan(e, axis: 1) >= 5f)
            .ToList();

        Assert.True(sheets.Count >= 4, $"expected bat wing/tip north-south sheet cuboids, found {sheets.Count}");
        Assert.All(sheets, e => Assert.InRange(AxisSpan(e, axis: 2), 0.08f, 0.15f));
    }

    [Fact]
    public void Allay_runtime_wing_sheets_use_thin_preview_thickness()
    {
        const string texturePath = "assets/minecraft/textures/entity/allay/allay.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out _));

        var sheets = merged.Elements
            .Where(e => HasOnlyFaces(e, "east", "west") &&
                        AxisSpan(e, axis: 1) >= 4f &&
                        AxisSpan(e, axis: 2) >= 6f)
            .ToList();

        Assert.True(sheets.Count >= 2, $"expected allay east-west wing sheet cuboids, found {sheets.Count}");
        Assert.All(sheets, e => Assert.InRange(AxisSpan(e, axis: 0), 0.08f, 0.15f));
    }

    [Fact]
    public void Allay_runtime_body_applies_static_negative_cube_deformation()
    {
        const string texturePath = "assets/minecraft/textures/entity/allay/allay.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out _));

        var bodyBase = merged.Elements.Single(e =>
            AxisSpan(e, axis: 0) is > 2.95f and < 3.05f &&
            AxisSpan(e, axis: 1) is > 3.95f and < 4.05f &&
            AxisSpan(e, axis: 2) is > 1.95f and < 2.05f);
        var bodyDeformed = merged.Elements.Single(e =>
            AxisSpan(e, axis: 0) is > 2.55f and < 2.75f &&
            AxisSpan(e, axis: 1) is > 4.55f and < 4.85f &&
            AxisSpan(e, axis: 2) is > 1.55f and < 1.75f);

        Assert.False(CuboidCornersMatch(bodyBase, bodyDeformed));
    }

    private static void AssertLocalAxisSpan(
        string cuboidJson,
        GeometryIrMeshEmitOptions options,
        int axis,
        float maxSpan,
        string label)
    {
        using var doc = JsonDocument.Parse(cuboidJson);
        Assert.True(
            EntityModelRuntime.TryToEntityCuboidForTests(
                doc.RootElement,
                options,
                out var entityCuboid,
                out var failure),
            failure);

        var b = new EntityModelRuntime.RigBuilder(options.AtlasWidth, options.AtlasHeight);
        entityCuboid.Emit(b, Matrix4x4.Identity, 1f);
        var el = Assert.Single(b.Build("entity/test").Elements);
        var span = axis switch
        {
            0 => MathF.Abs(el.To[0] - el.From[0]),
            1 => MathF.Abs(el.To[1] - el.From[1]),
            _ => MathF.Abs(el.To[2] - el.From[2]),
        };
        Assert.True(span <= maxSpan, $"{label} preview span {span:F3} exceeds {maxSpan:F3}");
    }

    private static float AxisSpan(ModelElement element, int axis) =>
        MathF.Abs(element.To[axis] - element.From[axis]);

    private static bool HasOnlyFaces(ModelElement element, params string[] faceNames)
    {
        if (element.Faces.Count != faceNames.Length)
        {
            return false;
        }

        foreach (var faceName in faceNames)
        {
            if (!element.Faces.ContainsKey(faceName))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CuboidCornersMatch(ModelElement a, ModelElement b)
    {
        for (var axis = 0; axis < 3; axis++)
        {
            if (MathF.Abs(a.From[axis] - b.From[axis]) > 0.001f ||
                MathF.Abs(a.To[axis] - b.To[axis]) > 0.001f)
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void Baby_chicken_full_mesh_wing_north_uv_matches_runtime_chicken_preset()
    {
        const string texturePath = "assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i], 64, 64, provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var atlasH = texSizes[texturePath].h;
        var wingNorth = new List<(float u, float v)>();
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6] * 64f;
            var v = verts[i + 7] * atlasH;
            if (u is >= 1.5f and <= 3.5f && v is >= 1.5f and <= 4.5f)
            {
                wingNorth.Add((u, v));
            }
        }

        Assert.True(wingNorth.Count >= 4, "expected left-wing north/south sheet verts in texCrop anchor region");
    }

    [Fact]
    public void Codegen_emit_left_wing_sheet_keeps_java_cube_north_south_uv_with_preview_thicken()
    {
        const string cuboid = """
            {
              "from": [-0.5, 0, 0],
              "to": [0.5, 2, 0],
              "uvOrigin": [2, 2],
              "faceMask": ["north", "south"],
              "uvSpan": [1, 2]
            }
            """;

        using var doc = JsonDocument.Parse(cuboid);
        var tableCuboid = new EntityModelRuntime.EntityCuboid(
            -0.5f, 0f, 0f, 0.5f, 2f, 0f, 2, 2, -1, -1, -1, false);
        var options = ChickenWingOptions() with
        {
            OfficialJvmName = "net.minecraft.client.model.animal.chicken.ChickenModel",
            PreferCodegenCuboids = true,
        };
        var entityCuboid = EntityModelRuntime.ApplyCodegenEmitOptionsForTests(
            tableCuboid,
            doc.RootElement,
            "left_wing",
            options);

        var b = new EntityModelRuntime.RigBuilder(options.AtlasWidth, options.AtlasHeight);
        entityCuboid.Emit(b, Matrix4x4.Identity, 1f);
        var built = b.Build("entity/test");
        var el = Assert.Single(built.Elements);
        Assert.True(el.Faces.TryGetValue("north", out var north));
        Assert.True(el.Faces.TryGetValue("south", out var south));
        AssertFaceCorner(north.Uv!, 2, 2);
        AssertFaceCorner(south.Uv!, 3, 2);
    }

    private static void AssertFaceCorner(float[] uv, float expectedU, float expectedV)
    {
        Assert.Contains(uv, u => MathF.Abs(u - expectedU) <= 0.05f);
        Assert.Contains(uv, v => MathF.Abs(v - expectedV) <= 0.05f);
    }

    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    private static GeometryIrMeshEmitOptions ChickenWingOptions() =>
        new()
        {
            Fidelity = GeometryIrEmitFidelity.Parity,
            AtlasWidth = 64,
            AtlasHeight = 32,
            OfficialJvmName = BabyChickenJvm,
            PreviewDegenerateAxisThickness = 0.06f,
        };

    private static void AssertFaceUv(
        string cuboidJson,
        GeometryIrMeshEmitOptions options,
        string partId,
        string faceName,
        float[] expectedUv)
    {
        using var doc = JsonDocument.Parse(cuboidJson);
        Assert.True(
            EntityModelRuntime.TryToEntityCuboidForTests(
                doc.RootElement,
                options with { OfficialJvmName = options.OfficialJvmName },
                out var entityCuboid,
                out var failure),
            failure);

        var b = new EntityModelRuntime.RigBuilder(options.AtlasWidth, options.AtlasHeight);
        entityCuboid.Emit(b, Matrix4x4.Identity, 1f);
        var built = b.Build("entity/test");
        var el = Assert.Single(built.Elements);
        Assert.True(el.Faces.TryGetValue(faceName, out var face), $"{partId}.{faceName}");
        for (var c = 0; c < 4; c++)
        {
            Assert.True(
                MathF.Abs(expectedUv[c] - face.Uv![c]) <= 0.05f,
                $"{partId}.{faceName}[{c}]: expected={expectedUv[c]} actual={face.Uv[c]}");
        }
    }
}
