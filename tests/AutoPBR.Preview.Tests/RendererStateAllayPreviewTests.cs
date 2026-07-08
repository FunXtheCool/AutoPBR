using System.Numerics;

namespace AutoPBR.Preview.Tests;

/// <summary>T0/T2: P6 Allay renderer-state shard + honest setupAnim hold/dance flags (no hand pose math).</summary>
public sealed class RendererStateAllayPreviewTests
{
    private const string AllayModel =
        "net.minecraft.client.model.animal.allay.AllayModel";

    private const string AllayRenderer =
        "net.minecraft.client.renderer.entity.AllayRenderer";

    [Fact]
    public void Allay_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(AllayRenderer, out var byRenderer));
        Assert.Equal(AllayRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(AllayModel, out var byModel));
        Assert.Equal("allay_hold_dance_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Allay_preview_state_cycles_holding_then_dance_spin_flags()
    {
        var atHold = PreviewRenderStateSynthesis.ForAllay(1.5f, 0.3f, 0.2f);
        Assert.True(atHold["holdingAnimationProgress"] > 0.4f);
        Assert.True(atHold["isDancing"] < 0.5f);

        var atDance = PreviewRenderStateSynthesis.ForAllay(5f, 0.3f, 0.2f);
        Assert.True(atDance["isDancing"] >= 0.5f);
        Assert.True(atDance["spinningProgress"] > 0.1f);
        Assert.True(atDance["holdingAnimationProgress"] < 0.1f);
    }

    [Fact]
    public void Allay_setup_anim_arm_pose_differs_between_hold_and_dance_phases()
    {
        var holdState = PreviewRenderStateSynthesis.ForAllay(1.5f, 0.3f, 0.2f);
        var danceState = PreviewRenderStateSynthesis.ForAllay(3.25f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(AllayModel, holdState, 1.5f, out var holdPose, "Allay", false));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(AllayModel, danceState, 3.25f, out var dancePose, "Allay", false));
        Assert.True(holdPose.Parts.TryGetValue("right_arm", out var holdArm));
        Assert.True(dancePose.Parts.TryGetValue("right_arm", out var danceArm));
        var delta = MathF.Abs(holdArm.ZRot - danceArm.ZRot) +
                    MathF.Abs(holdArm.YRot - danceArm.YRot);
        Assert.True(delta > 1e-4f, $"expected hold-vs-dance arm delta (delta={delta:F6})");
    }

    [Fact]
    public void Allay_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/allay/allay.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 1.5f,
            out var holdPhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 5f,
            out var dancePhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < holdPhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(holdPhase.Elements[i].LocalToParent),
                    Corner(dancePhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven allay mesh motion (max delta={maxDelta:F3})");
    }
}
