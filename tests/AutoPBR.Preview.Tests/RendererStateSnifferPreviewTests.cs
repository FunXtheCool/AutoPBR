using System.Numerics;

namespace AutoPBR.Preview.Tests;

/// <summary>T0/T2: P6 Sniffer renderer-state shard + honest setupAnim clip timing (no hand pose math).</summary>
public sealed class RendererStateSnifferPreviewTests
{
    private const string SnifferModel =
        "net.minecraft.client.model.animal.sniffer.SnifferModel";

    private const string SnifferRenderer =
        "net.minecraft.client.renderer.entity.SnifferRenderer";

    [Fact]
    public void Sniffer_renderer_state_shard_loads_from_data()
    {
        Assert.True(RendererStateDocumentLoader.TryLoadByRenderer(SnifferRenderer, out var byRenderer));
        Assert.Equal(SnifferRenderer, (string?)byRenderer["officialJvmName"]);
        Assert.True(RendererStateDocumentLoader.TryLoadForModel(SnifferModel, out var byModel));
        Assert.Equal("sniffer_clip_cycle", (string?)byModel["previewDriver"]);
    }

    [Fact]
    public void Sniffer_preview_state_cycles_walk_then_sniff_and_dig_clips()
    {
        var atWalk = PreviewRenderStateSynthesis.ForSniffer(1f, 0.3f, 0.2f);
        Assert.True(atWalk["walkAnimationSpeed"] > 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atWalk["sniffingAnimationState"]);

        var atSniff = PreviewRenderStateSynthesis.ForSniffer(2.5f, 0.3f, 0.2f);
        Assert.True(atSniff["sniffingAnimationState"] >= 0f && atSniff["sniffingAnimationState"] < 1f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, atSniff["diggingAnimationState"]);

        var atDig = PreviewRenderStateSynthesis.ForSniffer(4f, 0.3f, 0.2f);
        Assert.True(atDig["diggingAnimationState"] >= 0f && atDig["diggingAnimationState"] < 2f);
    }

    [Fact]
    public void Sniffer_setup_anim_head_pose_differs_between_walk_and_long_sniff_phases()
    {
        var walkState = PreviewRenderStateSynthesis.ForSniffer(1f, 0.3f, 0.2f);
        var sniffState = PreviewRenderStateSynthesis.ForSniffer(2.5f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(SnifferModel, walkState, 1f, out var walkPose, "Sniffer", false));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(SnifferModel, sniffState, 2.5f, out var sniffPose, "Sniffer", false));
        Assert.True(walkPose.Parts.TryGetValue("head", out var walkHead));
        Assert.True(sniffPose.Parts.TryGetValue("head", out var sniffHead));
        var delta = MathF.Abs(walkHead.XRot - sniffHead.XRot) +
                    MathF.Abs(walkHead.Y - sniffHead.Y) +
                    MathF.Abs(walkHead.Z - sniffHead.Z);
        Assert.True(delta > 1e-4f, $"expected long-sniff-phase head delta (delta={delta:F6})");
    }

    [Fact]
    public void Sniffer_geometry_ir_setup_anim_mesh_differs_across_preview_clock()
    {
        const string path = "assets/minecraft/textures/entity/sniffer/sniffer.png";
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 1f,
            out var walkPhase, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0.3f, animationTimeSeconds: 2.5f,
            out var sniffPhase, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < walkPhase.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(walkPhase.Elements[i].LocalToParent),
                    Corner(sniffPhase.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.02f, $"expected setupAnim-driven sniffer mesh motion (max delta={maxDelta:F3})");
    }
}
