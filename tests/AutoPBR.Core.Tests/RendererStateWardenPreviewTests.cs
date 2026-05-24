using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>T0/T2: P6 Warden renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateWardenPreviewTests
{
    private const string WardenModel =
        "net.minecraft.client.model.monster.warden.WardenModel";

    private const string WardenRenderer =
        "net.minecraft.client.renderer.entity.WardenRenderer";

    [Fact]
    public void Warden_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(WardenRenderer, out var byRenderer));
        Assert.Equal(WardenRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(WardenModel, out var byModel));
        Assert.Equal("warden_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Warden_preview_state_cycles_walk_then_sniff_emerge_roar()
    {
        var atWalk = PreviewRenderStateSynthesis.ForWarden(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["sniffAnimationState"]);

        var atSniff = PreviewRenderStateSynthesis.ForWarden(3f, 0.3f, 0.2f);
        Assert.True(atSniff["sniffAnimationState"] >= 0f && atSniff["sniffAnimationState"] < 2f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atSniff["roarAnimationState"]);

        var atEmerge = PreviewRenderStateSynthesis.ForWarden(5f, 0.3f, 0.2f);
        Assert.True(atEmerge["emergeAnimationState"] >= 0f && atEmerge["emergeAnimationState"] < 3f);

        var atRoar = PreviewRenderStateSynthesis.ForWarden(8f, 0.3f, 0.2f);
        Assert.True(atRoar["roarAnimationState"] >= 0f && atRoar["roarAnimationState"] < 2f);
    }

    [Fact]
    public void Warden_setup_anim_evaluates_when_mood_clip_active()
    {
        var sniffState = PreviewRenderStateSynthesis.ForWarden(3f, 0.3f, 0.2f);
        Assert.True(sniffState["sniffAnimationState"] >= 0f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(WardenModel, sniffState, 3f, out var pose, "Warden", false));
        Assert.NotEmpty(pose.Parts);
    }

    [Fact]
    public void Warden_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/warden/warden.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 5f,
            out var emergePhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 8f,
            out var roarPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < emergePhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(emergePhase.Elements[i].LocalToParent),
                    Corner(roarPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven warden mesh motion (max delta={maxDelta:F3})");
    }
}
