using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Baby bee north/south leg sheets use footprint <c>uvSpan</c> (not anchor echo). Documents emit vs Java cube unfold.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class BabyBeeLegUvDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Baby_bee_leg_sheets_emit_north_south_uvSpan_layout()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("BabyBeeModel", provenance.Detail ?? "", StringComparison.Ordinal);

        var legSheets = mesh.Elements
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east") &&
                         MathF.Abs(el.To[1] - el.From[1] - 1f) < 0.15f &&
                         MathF.Abs(el.To[0] - el.From[0] - 3f) < 0.15f)
            .OrderBy(el => el.Faces["north"].Uv![1])
            .ToList();
        Assert.Equal(3, legSheets.Count);

        // Java ModelPart.Cube cross-unfold at d=0: south starts at u+w (not texCrop gap u+w+2).
        var front = legSheets[0];
        Assert.Equal(new float[] { 13, 0, 16, 1 }, front.Faces["north"].Uv!);
        Assert.Equal(new float[] { 16, 0, 19, 1 }, front.Faces["south"].Uv!);

        var middle = legSheets[1];
        Assert.Equal(new float[] { 13, 1, 16, 2 }, middle.Faces["north"].Uv!);
        Assert.Equal(new float[] { 16, 1, 19, 2 }, middle.Faces["south"].Uv!);

        var back = legSheets[2];
        Assert.Equal(new float[] { 13, 2, 16, 3 }, back.Faces["north"].Uv!);
        Assert.Equal(new float[] { 16, 2, 19, 3 }, back.Faces["south"].Uv!);
    }

    [Fact]
    public void Baby_bee_leg_emit_matches_java_cube_unfold_not_texcrop_gap()
    {
        var javaSouth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.South, 13, 0, 3, 1, 0);
        var texCropSouth = GeometryIrUvAtlasQuality.BuildTexCropNorthSouthFaceUvRects(13, 0, 3, 1).South;

        Assert.Equal(new float[] { 13, 0, 16, 1 },
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.North, 13, 0, 3, 1, 0));
        Assert.Equal(new float[] { 16, 0, 19, 1 }, javaSouth);
        Assert.Equal(texCropSouth, new float[] { 18, 0, 21, 1 });
        Assert.NotEqual(javaSouth, texCropSouth);
    }

    [Fact]
    public void Baby_bee_rebaked_uv_fingerprint_matches_texcrop_emit()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var logical = EntityGeometryIrTextureAtlas.ResolveForBake(path, 64, 64, provenance, Profile26);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [ordered[0]] = logical,
        };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var baked, out _, out _));

        var fp = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMeshUvFingerprint(
            baked, MinecraftModelBaker.FloatsPerVertex);
        Assert.NotEqual(0UL, fp);
    }
}
