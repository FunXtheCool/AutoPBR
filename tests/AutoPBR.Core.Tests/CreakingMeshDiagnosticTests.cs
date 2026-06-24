using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class CreakingMeshDiagnosticTests
{
    private const string CreakingJvm = "net.minecraft.client.model.monster.creaking.CreakingModel";
    private const string TexturePath = "assets/minecraft/textures/entity/creaking/creaking.png";

    private static readonly MinecraftNativeProfile Profile26 = new(
        "26.1.2",
        Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
        new Version(26, 1, 2));

    [Fact]
    public void Creaking_parity_emit_matches_reference_java_mesh()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            $"{CreakingJvm}.json");
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));

        var parity = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/creaking/creaking", Profile26, CreakingJvm, 64, 64, out var err);
        Assert.Null(err);
        Assert.NotNull(parity);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(
            reference.RootElement, parity, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Creaking_catalog_emit_thins_head_side_sheet_z_like_bee_legs()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var sheets = SkinElements(mesh)
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east"))
            .ToList();
        Assert.Equal(2, sheets.Count);

        foreach (var sheet in sheets)
        {
            var spanZ = MathF.Abs(sheet.To[2] - sheet.From[2]);
            Assert.True(spanZ is >= 0.1f and < 0.2f,
                "head side sheet should use thin preview Z thicken (Java texCrop stays coplanar)");
        }
    }

    [Fact]
    public void Creaking_main_texture_merges_head_eyes_overlay()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        Assert.Equal(18, mesh.Elements.Count);
        var eyesOverlay = mesh.Elements
            .Where(el => el.Faces.Values.Any(f => string.Equals(f.TextureKey, "#eyes", StringComparison.Ordinal)))
            .ToList();
        Assert.Equal(2, eyesOverlay.Count);
        Assert.Contains("creaking_eyes", mesh.Textures["eyes"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Creaking_eyes_texture_emits_head_only()
    {
        const string eyesPath = "assets/minecraft/textures/entity/creaking/creaking_eyes.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(eyesPath, Profile26, 0f, 0f, out var mesh, out _), eyesPath);
        Assert.Equal(4, mesh.Elements.Count);
    }

    [Fact]
    public void Creaking_head_south_face_uv_matches_java_convention()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var head = SkinElements(mesh).Single(el =>
            el.Faces.ContainsKey("south") &&
            el.Faces.ContainsKey("north") &&
            el.Faces.ContainsKey("east") &&
            MathF.Abs(el.To[1] - el.From[1] - 10f) < 0.01f);

        var expectedSouth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.South, 0, 0, 6, 10, 6);
        Assert.Equal(expectedSouth, head.Faces["south"].Uv!);
    }

    [Fact]
    public void Creaking_head_side_sheets_use_uvSpan_north_south_layout()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var model), TexturePath);

        var sheets = SkinElements(model)
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east"))
            .ToList();
        Assert.Equal(2, sheets.Count);

        var east = sheets.Single(el => el.From[0] >= 0f);
        var west = sheets.Single(el => el.From[0] < 0f);

        Assert.Equal(new float[] { 12, 40, 21, 54 }, east.Faces["north"].Uv!);
        Assert.Equal(new float[] { 23, 40, 32, 54 }, east.Faces["south"].Uv!);
        Assert.Equal(new float[] { 34, 12, 43, 26 }, west.Faces["north"].Uv!);
        Assert.Equal(new float[] { 45, 12, 54, 26 }, west.Faces["south"].Uv!);
    }

    [Fact]
    public void Creaking_head_side_sheets_attach_to_head_in_preview_world()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var headCore = SkinElements(mesh).Single(el =>
            el.Faces.Count >= 6 &&
            MathF.Abs(el.To[1] - el.From[1] - 10f) < 0.01f &&
            MathF.Abs(el.To[0] - el.From[0] - 6f) < 0.01f);

        var sheets = SkinElements(mesh)
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east"))
            .ToList();
        Assert.Equal(2, sheets.Count);

        TransformWorldCorners(headCore, out var headMin, out var headMax);
        foreach (var sheet in sheets)
        {
            TransformWorldCorners(sheet, out var sheetMin, out var sheetMax);

            var sharesHeadY = MathF.Min(sheetMax.Y, headMax.Y) - MathF.Max(sheetMin.Y, headMin.Y) > 4f;
            Assert.True(sharesHeadY,
                $"head side sheet Y [{sheetMin.Y},{sheetMax.Y}] should overlap head [{headMin.Y},{headMax.Y}]");

            var touchesHeadX = sheetMax.X >= headMin.X - 0.05f && sheetMin.X <= headMax.X + 0.05f;
            var touchesHeadZ = sheetMax.Z >= headMin.Z - 0.05f && sheetMin.Z <= headMax.Z + 0.05f;
            Assert.True(touchesHeadX && touchesHeadZ,
                "head side sheet should share a head face (LER mirror swaps ±X extension side in world space)");

            var extendsBeyondHead =
                sheetMax.X > headMax.X + 0.5f || sheetMin.X < headMin.X - 0.5f ||
                sheetMax.Z > headMax.Z + 0.5f || sheetMin.Z < headMin.Z - 0.5f;
            Assert.True(extendsBeyondHead, "head side sheet should extend outward from the head shell");
        }
    }

    [Fact]
    public void Creaking_head_side_sheet_baked_uv_matches_uvSpan_layout()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var northCornerCount = 0;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (u >= 12f / 64f && u <= 21f / 64f && v >= 40f / 64f && v <= 54f / 64f)
            {
                northCornerCount++;
            }
        }

        Assert.True(northCornerCount >= 4, "expected baked verts sampling east head sheet north UV rect");
    }

    [Fact]
    public void Creaking_preview_world_corners_match_reference_java()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            $"{CreakingJvm}.json");
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CreakingJvm}.json");
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(CreakingJvm, shard.RootElement);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var fullMesh, out _), TexturePath);
        var mesh = BodySkinMesh(fullMesh);

        var cmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { PreviewDegenerateAxisThickness = 0.06f },
            tolerance: 0.35);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Creaking_leg_foot_disks_use_up_down_uvSpan_layout()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var footDisks = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") &&
                         el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, footDisks.Count);

        var left = footDisks.Single(el => el.To[0] > 2f);
        Assert.Equal(new float[] { 52, 55, 57, 63 }, left.Faces["down"].Uv!);
        Assert.Equal(new float[] { 45, 55, 50, 63 }, left.Faces["up"].Uv!);

        var right = footDisks.Single(el => el.From[0] <= -3f);
        Assert.Equal(new float[] { 52, 46, 57, 54 }, right.Faces["down"].Uv!);
        Assert.Equal(new float[] { 45, 46, 50, 54 }, right.Faces["up"].Uv!);
    }

    [Fact]
    public void Creaking_foot_disks_stay_coplanar_without_ground_shadow_layer()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out _), TexturePath);

        var footDisks = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") &&
                         el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, footDisks.Count);

        foreach (var disk in footDisks)
        {
            var spanY = MathF.Abs(disk.To[1] - disk.From[1]);
            Assert.True(spanY is >= 0.1f and < 0.2f,
                "foot disk should use thin Y preview thicken so up/down stay coplanar like Java");
        }
    }

    [Theory]
    [InlineData(0f, 0f, false)]
    [InlineData(0.3f, 2.5f, true)]
    public void Creaking_baked_preview_has_no_vertices_floating_left_of_torso(
        float idlePhase01, float animationTimeSeconds, bool applySetupAnimMotion)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath, Profile26, idlePhase01, animationTimeSeconds,
            out var model, out _, applySetupAnimMotion), TexturePath);

        Assert.Empty(CollectFloatingLeftTorsoVertices(model));
    }

    private static List<Vector3> CollectFloatingLeftTorsoVertices(MergedJavaBlockModel model)
    {
        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(model, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(model, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var floaters = new List<Vector3>();
        for (var i = 0; i < verts.Length; i += stride)
        {
            var p = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
            if (p.X < -6f && p.Y is > 0f and < 14f && MathF.Abs(p.Z) < 6f)
            {
                floaters.Add(p);
            }
        }

        return floaters;
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        var m = el.LocalToParent;
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (el.From[0], el.From[1], el.From[2]), (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]), (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]), (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]), (el.To[0], el.To[1], el.To[2]),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }

    private static IEnumerable<ModelElement> SkinElements(MergedJavaBlockModel model) =>
        model.Elements.Where(el =>
            el.Faces.Values.Any(f => string.Equals(f.TextureKey, "#skin", StringComparison.OrdinalIgnoreCase)));

    private static MergedJavaBlockModel BodySkinMesh(MergedJavaBlockModel model) =>
        new()
        {
            Elements = SkinElements(model).ToList(),
            Textures = model.Textures,
        };
}
