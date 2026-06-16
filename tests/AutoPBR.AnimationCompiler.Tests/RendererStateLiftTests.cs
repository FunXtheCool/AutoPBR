using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class RendererStateLiftTests
{
    [Fact]
    public void TryLift_extracts_models_animation_states_and_scalar_fields_from_javap_comments()
    {
        const string javap = """
                             public void extractRenderState(net.minecraft.world.entity.Entity, net.minecraft.client.renderer.entity.state.RabbitRenderState, float);
                               Code:
                                  0: new           #12 // class net/minecraft/client/model/animal/rabbit/RabbitModel
                                  3: getfield      #20 // Field net/minecraft/client/renderer/entity/state/RabbitRenderState.hopAnimationState:Lnet/minecraft/world/entity/AnimationState;
                                  6: invokevirtual #21 // Method net/minecraft/world/entity/AnimationState.copyFrom:(Lnet/minecraft/world/entity/AnimationState;)V
                                  9: getfield      #22 // Field net/minecraft/client/renderer/entity/state/RabbitRenderState.idleHeadTiltAnimationState:Lnet/minecraft/world/entity/AnimationState;
                                 12: getfield      #30 // Field net/minecraft/client/renderer/entity/state/RabbitRenderState.isBaby:Z
                                 15: getfield      #31 // Field net/minecraft/client/renderer/entity/state/RabbitRenderState.jumpCompletion:F
                             """;

        Assert.True(RendererStateLift.TryLift(
            javap,
            "net.minecraft.client.renderer.entity.RabbitRenderer",
            out var shard,
            out var notes), string.Join("; ", notes));

        Assert.Equal("net.minecraft.client.renderer.entity.RabbitRenderer", (string?)shard["officialJvmName"]);
        Assert.Equal("net.minecraft.client.renderer.entity.state.RabbitRenderState", (string?)shard["renderStateType"]);
        Assert.Equal("rabbit_clip_cycle", (string?)shard["previewDriver"]);
        Assert.Contains("RabbitModel", shard["modelJvmNames"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("hopAnimationState", shard["animationStateFields"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("jumpCompletion", shard["scalarRenderStateFields"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("isBaby", shard["scalarRenderStateFields"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TryLift_without_animation_states_uses_static_scalar_driver()
    {
        const string javap = """
                             public void extractRenderState(net.minecraft.world.entity.Entity, net.minecraft.client.renderer.entity.state.ChickenRenderState, float);
                               Code:
                                  0: new           #12 // class net/minecraft/client/model/animal/chicken/ChickenModel
                                  3: getfield      #20 // Field net/minecraft/client/renderer/entity/state/ChickenRenderState.flapSpeed:F
                                  6: getfield      #21 // Field net/minecraft/client/renderer/entity/state/ChickenRenderState.flap:F
                             """;

        Assert.True(RendererStateLift.TryLift(
            javap,
            "net.minecraft.client.renderer.entity.ChickenRenderer",
            out var shard,
            out var notes), string.Join("; ", notes));

        Assert.Equal("static_scalar_state", (string?)shard["previewDriver"]);
        Assert.Contains("flapSpeed", shard["scalarDefaults"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TryLift_extracts_supplementary_model_layers_from_renderer_javap()
    {
        const string javap = """
                             public void render(net.minecraft.client.renderer.entity.state.BreezeRenderState, float, float, float, float, float);
                               Code:
                                  0: invokevirtual #12 // Method net/minecraft/client/model/monster/breeze/BreezeModel.createWindLayer:()Lnet/minecraft/client/model/geom/builders/LayerDefinition;
                                  4: invokevirtual #13 // Method net/minecraft/client/model/monster/breeze/BreezeModel.createEyesLayer:()Lnet/minecraft/client/model/geom/builders/LayerDefinition;
                             """;

        Assert.True(RendererStateLift.TryLift(
            javap,
            "net.minecraft.client.renderer.entity.BreezeRenderer",
            out var shard,
            out _), string.Empty);

        var layers = shard["modelLayers"]!.ToJsonString();
        Assert.Contains("createWindLayer", layers, StringComparison.Ordinal);
        Assert.Contains("#wind", layers, StringComparison.Ordinal);
        Assert.Contains("cutoutOverlay", layers, StringComparison.Ordinal);
        Assert.Contains("createEyesLayer", layers, StringComparison.Ordinal);
        Assert.Contains("#eyes", layers, StringComparison.Ordinal);
        Assert.Contains("cosmeticOverlay", layers, StringComparison.Ordinal);
    }
}
