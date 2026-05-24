using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>T0/T2: P6 Camel renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateCamelPreviewTests
{
    private const string CamelModel =
        "net.minecraft.client.model.animal.camel.CamelModel";

    private const string CamelRenderer =
        "net.minecraft.client.renderer.entity.CamelRenderer";

    [Fact]
    public void Camel_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(CamelRenderer, out var byRenderer));
        Assert.Equal(CamelRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(CamelModel, out var byModel));
        Assert.Equal("camel_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Camel_preview_state_cycles_walk_then_sit_standup_dash_idle()
    {
        var atWalk = PreviewRenderStateSynthesis.ForCamel(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["sitAnimationState"]);

        var atSit = PreviewRenderStateSynthesis.ForCamel(3f, 0.3f, 0.2f);
        Assert.True(atSit["sitAnimationState"] >= 0f && atSit["sitAnimationState"] < 2f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atSit["dashAnimationState"]);

        var atStand = PreviewRenderStateSynthesis.ForCamel(6f, 0.3f, 0.2f);
        Assert.True(atStand["sitUpAnimationState"] >= 0f && atStand["sitUpAnimationState"] < 2.6f);

        var atDash = PreviewRenderStateSynthesis.ForCamel(7.8f, 0.3f, 0.2f);
        Assert.True(atDash["dashAnimationState"] >= 0f && atDash["dashAnimationState"] < 0.5f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atDash["idleAnimationState"]);
    }

    [Fact]
    public void Camel_setup_anim_tail_pose_differs_between_walk_and_idle_phases()
    {
        var walkState = PreviewRenderStateSynthesis.ForCamel(1f, 0.3f, 0.2f);
        var idleState = PreviewRenderStateSynthesis.ForCamel(9.5f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(CamelModel, walkState, 1f, out var walkPose, "Camel", false));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(CamelModel, idleState, 9.5f, out var idlePose, "Camel", false));
        Assert.True(walkPose.Parts.TryGetValue("tail", out var walkTail));
        Assert.True(idlePose.Parts.TryGetValue("tail", out var idleTail));
        var delta = MathF.Abs(walkTail.XRot - idleTail.XRot) +
                    MathF.Abs(walkTail.YRot - idleTail.YRot) +
                    MathF.Abs(walkTail.ZRot - idleTail.ZRot);
        Assert.True(delta > 1e-4f, $"expected idle-phase tail delta (delta={delta:F6})");
    }

    [Fact]
    public void Camel_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/camel/camel.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 1f,
            out var walkPhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 3f,
            out var sitPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < walkPhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(walkPhase.Elements[i].LocalToParent),
                    Corner(sitPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven camel mesh motion (max delta={maxDelta:F3})");
    }
}
