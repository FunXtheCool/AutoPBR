namespace AutoPBR.Preview.Tests;

/// <summary>T0/T2: P6 Nautilus renderer-state shard + honest setupAnim swim walk timing (no hand pose math).</summary>
public sealed class RendererStateNautilusPreviewTests
{
    private const string NautilusModel =
        "net.minecraft.client.model.animal.nautilus.NautilusModel";

    private const string NautilusRenderer =
        "net.minecraft.client.renderer.entity.NautilusRenderer";

    [Fact]
    public void Nautilus_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(NautilusRenderer, out var byRenderer));
        Assert.Equal(NautilusRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(NautilusModel, out var byModel));
        Assert.Equal("nautilus_swim_walk", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Nautilus_preview_state_provides_walk_fields_for_swim_applyWalk()
    {
        var state = PreviewRenderStateSynthesis.ForNautilus(1.5f, 0.3f, 0.2f);
        Assert.True(state["walkAnimationSpeed"] > 0f);
        Assert.True(state["walkAnimationPos"] != 0f);
        Assert.True(state["ageInTicks"] > 0f);
    }

    [Fact]
    public void Nautilus_setup_anim_evaluates_with_lifted_shard()
    {
        var state = PreviewRenderStateSynthesis.ForNautilus(2f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(NautilusModel, state, 2f, out var pose, "NautilusMob", false));
        Assert.NotEmpty(pose.Parts);
    }
}
