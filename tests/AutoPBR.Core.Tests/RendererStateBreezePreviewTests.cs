using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>T0/T2: P6 Breeze renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateBreezePreviewTests
{
    private const string BreezeModel =
        "net.minecraft.client.model.monster.breeze.BreezeModel";

    private const string BreezeRenderer =
        "net.minecraft.client.renderer.entity.BreezeRenderer";

    [Fact]
    public void Breeze_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(BreezeRenderer, out var byRenderer));
        Assert.Equal(BreezeRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(BreezeModel, out var byModel));
        Assert.Equal("breeze_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Breeze_preview_state_cycles_shoot_slide_jump_inhale_clips()
    {
        var atIdle = PreviewRenderStateSynthesis.ForBreeze(0.5f, 0.3f, 0.2f);
        Assert.True(atIdle["idle"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atIdle["shoot"]);

        var atShoot = PreviewRenderStateSynthesis.ForBreeze(2.5f, 0.3f, 0.2f);
        Assert.True(atShoot["shoot"] >= 0f && atShoot["shoot"] < 1.125f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atShoot["slide"]);

        var atSlide = PreviewRenderStateSynthesis.ForBreeze(3.2f, 0.3f, 0.2f);
        Assert.True(atSlide["slide"] >= 0f && atSlide["slide"] < 0.2f);

        var atJump = PreviewRenderStateSynthesis.ForBreeze(5.6f, 0.3f, 0.2f);
        Assert.True(atJump["longJump"] >= 0f && atJump["longJump"] < 0.5f);
    }

    [Fact]
    public void Geometry_ir_setup_anim_state_uses_renderer_state_when_model_has_p6_shard()
    {
        var state = CleanRoomEntityModelRuntime.ResolveSetupAnimPreviewStateForTests(
            BreezeModel,
            animationTimeSeconds: 2.5f,
            idlePhase01: 0.3f,
            wave: 0.2f,
            out var source);

        Assert.Equal("renderer-state", source);
        Assert.True(state["shoot"] >= 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, state["slide"]);
    }

    [Fact]
    public void Breeze_setup_anim_head_pose_differs_between_idle_and_shoot_phases()
    {
        var idleState = PreviewRenderStateSynthesis.ForBreeze(0.5f, 0.3f, 0.2f);
        var shootState = PreviewRenderStateSynthesis.ForBreeze(2.5f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(BreezeModel, idleState, 0.5f, out var idlePose, "Breeze", false));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(BreezeModel, shootState, 2.5f, out var shootPose, "Breeze", false));
        Assert.True(idlePose.Parts.TryGetValue("head", out var idleHead));
        Assert.True(shootPose.Parts.TryGetValue("head", out var shootHead));
        var delta = MathF.Abs(idleHead.XRot - shootHead.XRot) +
                    MathF.Abs(idleHead.Y - shootHead.Y) +
                    MathF.Abs(idleHead.Z - shootHead.Z);
        Assert.True(delta > 1e-4f, $"expected shoot-phase head delta (delta={delta:F6})");
    }

    [Fact]
    public void Breeze_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/breeze/breeze.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 0.5f,
            out var early, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 2.5f,
            out var shootPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < early.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(early.Elements[i].LocalToParent),
                    Corner(shootPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven breeze mesh motion (max delta={maxDelta:F3})");
    }
}
