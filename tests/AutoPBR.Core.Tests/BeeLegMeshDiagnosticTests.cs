using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BeeLegMeshDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Adult_bee_leg_sheets_use_north_south_uvSpan_layout()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("AdultBeeModel", provenance.Detail ?? "", StringComparison.Ordinal);

        var legSheets = mesh.Elements
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east") &&
                         MathF.Abs(el.To[1] - el.From[1] - 2f) < 0.15f &&
                         MathF.Abs(el.To[0] - el.From[0] - 7f) < 0.15f)
            .ToList();
        Assert.Equal(3, legSheets.Count);

        var front = legSheets.Single(el => el.Faces["north"].Uv![1] < 2f);
        Assert.Equal(new float[] { 26, 1, 33, 3 }, front.Faces["north"].Uv!);
        Assert.Equal(new float[] { 35, 1, 42, 3 }, front.Faces["south"].Uv!);

        var middle = legSheets.Single(el =>
            MathF.Abs(el.Faces["north"].Uv![1] - 3f) < 0.01f);
        Assert.Equal(new float[] { 26, 3, 33, 5 }, middle.Faces["north"].Uv!);
        Assert.Equal(new float[] { 35, 3, 42, 5 }, middle.Faces["south"].Uv!);

        var back = legSheets.Single(el =>
            MathF.Abs(el.Faces["north"].Uv![1] - 5f) < 0.01f);
        Assert.Equal(new float[] { 26, 5, 33, 7 }, back.Faces["north"].Uv!);
        Assert.Equal(new float[] { 35, 5, 42, 7 }, back.Faces["south"].Uv!);
    }

    [Fact]
    public void Adult_bee_leg_sheets_have_preview_z_thickness()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _), path);

        var legSheets = mesh.Elements
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east") &&
                         MathF.Abs(el.To[1] - el.From[1] - 2f) < 0.15f)
            .ToList();
        Assert.Equal(3, legSheets.Count);

        foreach (var sheet in legSheets)
        {
            Assert.True(MathF.Abs(sheet.To[2] - sheet.From[2]) >= 0.1f,
                "bee leg sheet should expand zero Z before preview emit");
        }
    }

    [Fact]
    public void Adult_bee_catalog_mesh_matches_reference_java_cuboids()
    {
        const string beeJvm = "net.minecraft.client.model.animal.bee.AdultBeeModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            $"{beeJvm}.json");
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        using var shard = LoadBeeShard();

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, shard.RootElement, tolerance: 0.12);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    private static JsonDocument LoadBeeShard()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.bee.AdultBeeModel.json");
        return JsonDocument.Parse(File.ReadAllText(shardPath));
    }

    [Fact]
    public void Adult_bee_emit_uses_model_part_block_stack_not_column_part_pose()
    {
        const string beeJvm = "net.minecraft.client.model.animal.bee.AdultBeeModel";
        var opts = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = beeJvm };
        using var wingPose = JsonDocument.Parse("""
            {"translation":[0,0,0],"rotationEulerRad":[0,-0.2618,0],"eulerOrder":"XYZ"}
            """);
        Assert.False(CleanRoomEntityModelRuntime.ShouldUseColumnPartPoseCompose(
            wingPose.RootElement, opts with { OfficialJvmName = beeJvm }));
    }

    [Fact]
    public void Adult_bee_wing_world_corners_stay_near_body()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _), path);

        var wings = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") && el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, wings.Count);

        var body = mesh.Elements
            .Single(el => el.Faces.Count >= 6 &&
                          MathF.Abs(el.To[1] - el.From[1] - 7f) < 0.2f &&
                          MathF.Abs(el.To[0] - el.From[0] - 7f) < 0.2f);

        foreach (var wing in wings)
        {
            TransformWorldCorners(body, out var bodyMin, out var bodyMax);
            TransformWorldCorners(wing, out var wingMin, out var wingMax);
            Assert.True(MathF.Abs(wingMax.Y - bodyMax.Y) < 1f,
                $"wing top Y {wingMax.Y} should attach near body top Y {bodyMax.Y}");
            var overlapX = MathF.Min(wingMax.X, bodyMax.X) - MathF.Max(wingMin.X, bodyMin.X);
            Assert.True(overlapX >= 2f,
                $"wing X range [{wingMin.X},{wingMax.X}] should overlap body [{bodyMin.X},{bodyMax.X}]");
        }
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

    [Fact]
    public void Adult_bee_wing_sheets_attach_to_body_in_preview_world()
    {
        const string beeJvm = "net.minecraft.client.model.animal.bee.AdultBeeModel";
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            $"{beeJvm}.json");
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{beeJvm}.json");
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(beeJvm, shard.RootElement);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _), path);

        var emitOptions = CleanRoomEntityModelRuntime.CreateParityCatalogPartIdResolveEmitOptions(
            "Bee", Profile26, isBaby: false, beeJvm, 64, 64, path, previewPoseId: null);
        var cmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement,
            repaired,
            mesh,
            emitOptions,
            tolerance: 0.35);
        Assert.True(cmp.IsMatch, cmp.Message);

        var wings = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") && el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, wings.Count);

        var body = mesh.Elements
            .Single(el => el.Faces.Count >= 6 &&
                          MathF.Abs(el.To[1] - el.From[1] - 7f) < 0.2f &&
                          MathF.Abs(el.To[0] - el.From[0] - 7f) < 0.2f);

        foreach (var wing in wings)
        {
            TransformWorldCorners(body, out var bodyMin, out var bodyMax);
            TransformWorldCorners(wing, out var wingMin, out var wingMax);
            Assert.True(MathF.Abs(wingMax.Y - bodyMax.Y) < 1f,
                $"wing top Y {wingMax.Y} should attach near body top Y {bodyMax.Y}");
            var overlapX = MathF.Min(wingMax.X, bodyMax.X) - MathF.Max(wingMin.X, bodyMin.X);
            Assert.True(overlapX >= 2f,
                $"wing X range [{wingMin.X},{wingMax.X}] should overlap body [{bodyMin.X},{bodyMax.X}]");
        }
    }

    [Fact]
    public void Adult_bee_wing_sheets_use_texCrop_up_anchor_uv_at_origin()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _), path);

        var wings = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") && el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, wings.Count);

        var (expectedUp, expectedDown) = GeometryIrUvAtlasQuality.BuildUpDownTexCropFaceUvRects(
            0, 18, 9, 6, ["up", "down"]);

        foreach (var wing in wings)
        {
            Assert.Equal(expectedUp, wing.Faces["up"].Uv!);
            Assert.Equal(expectedDown, wing.Faces["down"].Uv!);
        }

        var mirrored = wings.Single(w => w.MirrorCuboidUv);
        Assert.Equal(expectedUp, mirrored.Faces["up"].Uv!);
    }

    [Fact]
    public void Adult_bee_wings_apply_setup_anim_motion_without_emit_failure()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 2.5f,
            out var mesh, out _, applyGeometryIrSetupAnimMotion: true), path);

        var wings = mesh.Elements
            .Where(el => el.Faces.ContainsKey("up") && el.Faces.ContainsKey("down") &&
                         !el.Faces.ContainsKey("north"))
            .ToList();
        Assert.Equal(2, wings.Count);
    }
}
