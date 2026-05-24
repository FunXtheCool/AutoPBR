namespace AutoPBR.Core.Tests;

/// <summary>T0/T2: P6 CopperGolem renderer-state shard + honest setupAnim walk/idle timing (no hand pose math).</summary>
public sealed class RendererStateCopperGolemPreviewTests
{
    private const string CopperGolemModel =
        "net.minecraft.client.model.animal.golem.CopperGolemModel";

    private const string CopperGolemRenderer =
        "net.minecraft.client.renderer.entity.CopperGolemRenderer";

    [Fact]
    public void CopperGolem_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(CopperGolemRenderer, out var byRenderer));
        Assert.Equal(CopperGolemRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(CopperGolemModel, out var byModel));
        Assert.Equal("copper_golem_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void CopperGolem_preview_state_cycles_walk_then_idle()
    {
        var atWalk = PreviewRenderStateSynthesis.ForCopperGolem(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["idleAnimationState"]);

        var atIdle = PreviewRenderStateSynthesis.ForCopperGolem(3f, 0.3f, 0.2f);
        Assert.True(atIdle["idleAnimationState"] >= 0f && atIdle["idleAnimationState"] < 3f);
    }

    [Fact]
    public void CopperGolem_setup_anim_evaluates_when_idle_clip_active()
    {
        var idleState = PreviewRenderStateSynthesis.ForCopperGolem(3f, 0.3f, 0.2f);
        Assert.True(idleState["idleAnimationState"] >= 0f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(CopperGolemModel, idleState, 3f, out var pose, "CopperGolem", false));
        Assert.NotEmpty(pose.Parts);
    }
}
