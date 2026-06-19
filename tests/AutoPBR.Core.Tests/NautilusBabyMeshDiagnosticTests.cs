using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class NautilusBabyMeshDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Nautilus_baby_mesh_is_compact_not_scattered()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            Profile26,
            0f,
            0f,
            out var mesh,
            out var provenance));

        Assert.Contains("NautilusModel", provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.Contains("createBabyBodyLayer", provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.True(mesh.Elements.Count >= 6, $"expected full IR rig, got {mesh.Elements.Count} elements");

        var hasBabyShellWidth = mesh.Elements.Any(e => MathF.Abs(e.From[0] - (-6f)) < 0.01f);
        Assert.True(hasBabyShellWidth, "expected baby shell cuboid width (from.x ~= -6), not adult -7");

        var compactness = ComputeMeshOriginCompactness(mesh);
        Assert.True(
            compactness.MaxOriginDistanceFromCentroid < 28f,
            $"parts too scattered (max origin distance {compactness.MaxOriginDistanceFromCentroid:F2}, span {compactness.BoundingSpan:F2}, elements {mesh.Elements.Count})");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1.5f)]
    public void Nautilus_baby_mesh_stays_compact_with_setup_anim_motion(float animationTimeSeconds)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            Profile26,
            animationTimeSeconds,
            0.3f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: true));

        var compactness = ComputeMeshOriginCompactness(mesh);
        Assert.True(
            compactness.MaxOriginDistanceFromCentroid < 28f,
            $"t={animationTimeSeconds}: parts too scattered (max {compactness.MaxOriginDistanceFromCentroid:F2}, span {compactness.BoundingSpan:F2}, n={mesh.Elements.Count})");
    }

    [Fact]
    public void Nautilus_catalog_preview_uses_geometry_ir_driver()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            Profile26,
            0f,
            0f,
            out var mesh,
            out var provenance));

        Assert.Contains("NautilusModel", provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.Equal(8, mesh.Elements.Count);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
    }

    [Fact]
    public void Nautilus_baby_uses_64x64_layer_atlas_from_createBabyBodyLayer()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            Profile26,
            0f,
            0f,
            out var mesh,
            out var provenance));

        Assert.Contains("createBabyBodyLayer", provenance.Detail ?? "", StringComparison.Ordinal);

        var rule = EntityTextureParityCatalog.ResolveRule(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            "nautilus_baby");
        Assert.NotNull(rule);
        Assert.Equal(64, rule!.GeometryIrTextureWidth);
        Assert.Equal(64, rule.GeometryIrTextureHeight);

        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26,
                rule,
                "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
                "nautilus_baby",
                isBaby: true,
                out _,
                out var geometryRoot));
        Assert.Equal(64, geometryRoot.GetProperty("textureWidth").GetInt32());
        Assert.Equal(64, geometryRoot.GetProperty("textureHeight").GetInt32());
    }

    [Fact]
    public void Nautilus_baby_world_span_is_smaller_than_adult_mesh()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus.png",
            Profile26,
            0f,
            0f,
            out var adult,
            out _));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/nautilus/nautilus_baby.png",
            Profile26,
            0f,
            0f,
            out var baby,
            out _));

        var adultSpan = ComputeMeshOriginCompactness(adult).BoundingSpan;
        var babySpan = ComputeMeshOriginCompactness(baby).BoundingSpan;
        Assert.True(babySpan < adultSpan * 0.92f, $"baby span {babySpan:F2} vs adult {adultSpan:F2}");
        Assert.True(babySpan > adultSpan * 0.25f, $"baby span {babySpan:F2} too small vs adult {adultSpan:F2}");
    }

    private static (float MaxOriginDistanceFromCentroid, float BoundingSpan) ComputeMeshOriginCompactness(MergedJavaBlockModel model)
    {
        var origins = new List<Vector3>(model.Elements.Count);
        foreach (var el in model.Elements)
        {
            var c = new Vector3(
                (el.From[0] + el.To[0]) * 0.5f,
                (el.From[1] + el.To[1]) * 0.5f,
                (el.From[2] + el.To[2]) * 0.5f);
            origins.Add(Vector3.Transform(c, el.LocalToParent));
        }

        var centroid = Vector3.Zero;
        foreach (var o in origins)
        {
            centroid += o;
        }

        centroid /= origins.Count;

        var maxDist = 0f;
        var min = origins[0];
        var max = origins[0];
        foreach (var o in origins)
        {
            maxDist = MathF.Max(maxDist, Vector3.Distance(o, centroid));
            min = Vector3.Min(min, o);
            max = Vector3.Max(max, o);
        }

        var span = MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z));
        return (maxDist, span);
    }
}
