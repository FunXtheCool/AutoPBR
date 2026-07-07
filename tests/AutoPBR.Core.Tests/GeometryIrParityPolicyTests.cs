

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrParityPolicyTests
{
    [Fact]
    public void Policy_default_tier_is_prefer_ir_on_disk()
    {
        GeometryIrParityPolicy.ResetForTests();
        Assert.Equal(GeometryIrParityTier.PreferIr, GeometryIrParityPolicy.DefaultTier);
        Assert.Equal(GeometryIrParityTier.PreferIr, GeometryIrParityPolicy.GetTier("Zombie"));
        Assert.Equal(GeometryIrParityTier.IrGeometryPreviewAnim, GeometryIrParityPolicy.GetTier("Pig"));
    }

    [Fact]
    public void Policy_chicken_uses_ir_geometry_preview_anim_on_disk()
    {
        GeometryIrParityPolicy.ResetForTests();
        Assert.Equal(GeometryIrParityTier.IrGeometryPreviewAnim, GeometryIrParityPolicy.GetTier("Chicken"));
    }

    [Fact]
    public void Policy_catalog_quadrupeds_use_ir_geometry_preview_anim_on_disk()
    {
        GeometryIrParityPolicy.ResetForTests();
        foreach (var builder in new[] { "Cat", "Fox", "Wolf", "Goat", "Cow" })
        {
            Assert.Equal(GeometryIrParityTier.IrGeometryPreviewAnim, GeometryIrParityPolicy.GetTier(builder));
        }
    }

    [Fact]
    public void Chicken_ir_preview_anim_mesh_depends_on_animation_clock()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        const string path = "assets/minecraft/textures/entity/chicken/chicken_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(
            path, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var atRest,
            out _, applyGeometryIrSetupAnimMotion: true));
        Assert.True(runtime.TryBuildStaticMesh(
            path, profile, idlePhase01: 0.77f, animationTimeSeconds: 1.91f, out var animated,
            out _, applyGeometryIrSetupAnimMotion: true));
        Assert.NotEqual(atRest.Elements[0].LocalToParent, animated.Elements[0].LocalToParent);
    }
}
