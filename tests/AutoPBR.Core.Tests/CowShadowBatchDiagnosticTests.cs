using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class CowShadowBatchDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Fact]
    public void Cow_main_torso_cuboid_draws_into_shadow_batches()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _));

        var torso = mesh.Elements.Single(e =>
            e.From[0] == -6f && e.To[0] == 6f && e.From[1] == -10f && e.To[1] == 8f);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, torso.DepthLayerKind);
        Assert.True(torso.CastsShadow);

        var tex = new Dictionary<string, int> { [path] = 0 };
        var sizes = new Dictionary<string, (int w, int h)> { [path] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", tex, sizes, out _, out _, out var batches));

        var torsoBatch = batches.Where(b =>
            b.LayerPolicy.Kind == PreviewDepthLayerKind.CutoutOverlay &&
            b.LayerPolicy.ShadowMode == PreviewDrawLayerShadowMode.Draw).ToList();
        Assert.NotEmpty(torsoBatch);
    }

    [Fact]
    public void Cow_horns_cast_shadows_as_cutout_geometry()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _));

        var horns = mesh.Elements
            .Where(e => e.Faces.Values.Any(f => f.TextureKey is "#right_horn" or "#left_horn"))
            .ToList();
        Assert.Equal(2, horns.Count);
        Assert.All(horns, h =>
        {
            Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, h.DepthLayerKind);
            Assert.True(h.CastsShadow);
        });

        var tex = new Dictionary<string, int> { [path] = 0 };
        var sizes = new Dictionary<string, (int w, int h)> { [path] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", tex, sizes, out _, out _, out var batches));
        Assert.Contains(batches, b =>
            b.LayerPolicy.Kind == PreviewDepthLayerKind.CutoutOverlay &&
            b.LayerPolicy.ShadowMode == PreviewDrawLayerShadowMode.Draw &&
            b.IndexCount == 36);
    }

    [Fact]
    public void Slime_outer_cube_casts_shadows_while_staying_translucent_overlay()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/slime/slime.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _));

        var outer = mesh.Elements.SingleOrDefault(e =>
            e.DepthLayerKind == PreviewDepthLayerKind.TranslucentOverlay);
        Assert.NotNull(outer);
        Assert.True(outer!.CastsShadow);

        var tex = new Dictionary<string, int> { [path] = 0 };
        var sizes = new Dictionary<string, (int w, int h)> { [path] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", tex, sizes, out _, out _, out var batches));
        Assert.Contains(batches, b =>
            b.LayerPolicy.Kind == PreviewDepthLayerKind.TranslucentOverlay &&
            b.LayerPolicy.ShadowMode == PreviewDrawLayerShadowMode.Draw);
    }

    [Fact]
    public void EnderDragon_baked_bounds_exceed_legacy_shadow_ortho_extent()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/enderdragon/dragon.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _));

        var tex = new Dictionary<string, int> { [path] = 0 };
        var sizes = new Dictionary<string, (int w, int h)> { [path] = (256, 256) };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", tex, sizes, out var verts, out _, out _));

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        for (var i = 0; i + 2 < verts.Length; i += MinecraftModelBaker.FloatsPerVertex)
        {
            var p = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var half = MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z)) * 0.5f;
        Assert.True(half > 3f, $"dragon preview bounds need a wide shadow map (half={half:F2})");
    }

    [Fact]
    public void EnderDragon_coplanar_outer_shells_cast_shadows()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/enderdragon/dragon.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out _));

        var shadowCastingOverlays = mesh.Elements
            .Where(e => e.DepthLayerKind == PreviewDepthLayerKind.CutoutOverlay && e.CastsShadow)
            .ToList();
        Assert.NotEmpty(shadowCastingOverlays);
    }
}
