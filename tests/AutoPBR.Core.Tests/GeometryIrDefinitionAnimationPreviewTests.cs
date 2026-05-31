using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrDefinitionAnimationPreviewTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Armadillo_geometry_ir_walk_tail_differs_from_bind_at_quarter_second()
    {
        const string path = "assets/minecraft/textures/entity/armadillo/armadillo.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.2f, animationTimeSeconds: 0f,
            out var bind, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.2f, animationTimeSeconds: 0.25f,
            out var walk, out _, applyGeometryIrSetupAnimMotion: true));

        var tailIdx = FindTailElementIndex(bind);
        Assert.True(tailIdx >= 0, "expected tail cuboid on adult armadillo geometry IR mesh");

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var delta = Vector3.Distance(
            Corner(bind.Elements[tailIdx].LocalToParent),
            Corner(walk.Elements[tailIdx].LocalToParent));
        Assert.True(delta > 0.02f, $"tail should move with ARMADILLO_WALK IR (delta={delta:F4})");
    }

    [Fact]
    public void Breeze_wind_geometry_ir_idle_wind_mid_differs_over_time()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze_wind.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var atZero, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0.5f,
            out var atHalf, out _, applyGeometryIrSetupAnimMotion: true));

        Assert.True(TryMaxCornerDelta(atZero, atHalf, out var maxDelta));
        Assert.True(maxDelta > 0.05f, $"wind tier should animate (max corner delta={maxDelta:F3})");
    }

    [Fact]
    public void Breeze_geometry_ir_shoot_head_differs_from_bind()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0.25f,
            out var shoot, out _, applyGeometryIrSetupAnimMotion: true));

        Assert.True(TryMaxCornerDelta(bind, shoot, out var maxDelta));
        Assert.True(maxDelta > 0.02f, $"head SHOOT channels should move IR mesh (max delta={maxDelta:F3})");
    }

    [Fact]
    public void Baby_fox_geometry_ir_walk_right_hind_leg_differs_from_bind()
    {
        const string path = "assets/minecraft/textures/entity/fox/fox_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.25f, animationTimeSeconds: 0f,
            out var bind, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.25f, animationTimeSeconds: 0.25f,
            out var walk, out _, applyGeometryIrSetupAnimMotion: true));

        Assert.True(TryMaxCornerDelta(bind, walk, out var maxDelta));
        Assert.True(maxDelta > 0.02f, $"baby fox FOX_BABY_WALK IR should move mesh (max delta={maxDelta:F4})");
    }

    [Fact]
    public void Armadillo_static_bind_pose_ignores_definition_animation_when_setup_anim_off()
    {
        const string path = "assets/minecraft/textures/entity/armadillo/armadillo.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.2f, animationTimeSeconds: 0f,
            out var atZero, out _, applyGeometryIrSetupAnimMotion: false));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.2f, animationTimeSeconds: 0.25f,
            out var atQuarter, out _, applyGeometryIrSetupAnimMotion: false));

        Assert.True(TryMaxCornerDelta(atZero, atQuarter, out var maxDelta));
        Assert.True(maxDelta <= 1e-5f, $"bind pose should stay static when setupAnim/definition pass is off (max delta={maxDelta:F6})");
    }

    private static bool TryMaxCornerDelta(MergedJavaBlockModel a, MergedJavaBlockModel b, out float maxDelta)
    {
        maxDelta = 0f;
        if (a.Elements.Count != b.Elements.Count)
        {
            return false;
        }

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        for (var i = 0; i < a.Elements.Count; i++)
        {
            maxDelta = MathF.Max(maxDelta, Vector3.Distance(
                Corner(a.Elements[i].LocalToParent),
                Corner(b.Elements[i].LocalToParent)));
        }

        return true;
    }

    private static int FindTailElementIndex(MergedJavaBlockModel model)
    {
        for (var i = 0; i < model.Elements.Count; i++)
        {
            var e = model.Elements[i];
            var w = e.To[0] - e.From[0];
            var h = e.To[1] - e.From[1];
            var d = e.To[2] - e.From[2];
            if (w <= 1.1f && h >= 5f && d <= 1.2f)
            {
                return i;
            }
        }

        return -1;
    }

}
