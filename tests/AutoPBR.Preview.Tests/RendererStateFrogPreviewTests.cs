using System.Numerics;

namespace AutoPBR.Preview.Tests;

/// <summary>T0/T2: P6 Frog renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateFrogPreviewTests
{
    private const string FrogModel =
        "net.minecraft.client.model.animal.frog.FrogModel";

    private const string FrogRenderer =
        "net.minecraft.client.renderer.entity.FrogRenderer";

    [Fact]
    public void Frog_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(FrogRenderer, out var byRenderer));
        Assert.Equal(FrogRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(FrogModel, out var byModel));
        Assert.Equal("frog_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Frog_preview_state_cycles_walk_then_croak_tongue()
    {
        var atWalk = PreviewRenderStateSynthesis.ForFrog(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["croakAnimationState"]);

        var atCroak = PreviewRenderStateSynthesis.ForFrog(3.5f, 0.3f, 0.2f);
        Assert.True(atCroak["croakAnimationState"] >= 0f && atCroak["croakAnimationState"] < 3f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atCroak["tongueAnimationState"]);

        var atTongue = PreviewRenderStateSynthesis.ForFrog(5.75f, 0.3f, 0.2f);
        Assert.True(atTongue["tongueAnimationState"] >= 0f && atTongue["tongueAnimationState"] < 0.5f);
    }

    [Fact]
    public void Frog_setup_anim_evaluates_when_croak_clip_active()
    {
        var croakState = PreviewRenderStateSynthesis.ForFrog(3.5f, 0.3f, 0.2f);
        Assert.True(croakState["croakAnimationState"] >= 0f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(FrogModel, croakState, 3.5f, out var pose, "Frog", false));
        Assert.NotEmpty(pose.Parts);
    }

    [Fact]
    public void Frog_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/frog/frog_temperate.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 1f,
            out var walkPhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 3.5f,
            out var croakPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < walkPhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(walkPhase.Elements[i].LocalToParent),
                    Corner(croakPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven frog mesh motion (max delta={maxDelta:F3})");
    }
}
