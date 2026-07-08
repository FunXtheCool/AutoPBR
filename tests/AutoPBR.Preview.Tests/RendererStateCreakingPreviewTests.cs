using System.Numerics;

namespace AutoPBR.Preview.Tests;

/// <summary>T0/T2: P6 Creaking renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateCreakingPreviewTests
{
    private const string CreakingModel =
        "net.minecraft.client.model.monster.creaking.CreakingModel";

    private const string CreakingRenderer =
        "net.minecraft.client.renderer.entity.CreakingRenderer";

    [Fact]
    public void Creaking_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(CreakingRenderer, out var byRenderer));
        Assert.Equal(CreakingRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(CreakingModel, out var byModel));
        Assert.Equal("creaking_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Creaking_preview_state_cycles_walk_then_attack()
    {
        var atWalk = PreviewRenderStateSynthesis.ForCreaking(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["attackAnimationState"]);

        var atAttack = PreviewRenderStateSynthesis.ForCreaking(2.5f, 0.3f, 0.2f);
        Assert.True(atAttack["attackAnimationState"] >= 0f && atAttack["attackAnimationState"] < 0.7083f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atAttack["deathAnimationState"]);
    }

    [Fact]
    public void Creaking_setup_anim_evaluates_when_attack_clip_active()
    {
        var attackState = PreviewRenderStateSynthesis.ForCreaking(2.5f, 0.3f, 0.2f);
        Assert.True(attackState["attackAnimationState"] >= 0f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(CreakingModel, attackState, 2.5f, out var pose, "Creaking", false));
        Assert.NotEmpty(pose.Parts);
    }

    [Fact]
    public void Creaking_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/creaking/creaking.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 1f,
            out var walkPhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 2.5f,
            out var attackPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < walkPhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(walkPhase.Elements[i].LocalToParent),
                    Corner(attackPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven creaking mesh motion (max delta={maxDelta:F3})");
    }
}
