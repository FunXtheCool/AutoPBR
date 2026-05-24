using System.Text.Json.Nodes;

using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class AnimationClinitLiftTests
{
    private static string ArmadilloJavapPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            "generated",
            "minecraft-client-model-index-26.1.2-animation-init",
            "net_minecraft_client_animation_definitions_ArmadilloAnimation.javapc.txt");

    [Fact]
    public void TryLift_Armadillo_javap_has_four_definitions_roll_up_length_and_scoped_channels()
    {
        var text = File.ReadAllText(ArmadilloJavapPath);
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        Assert.Equal(4, defs.Count);

        var d0 = defs[0]!.AsObject();
        Assert.Equal("ARMADILLO_ROLL_UP", (string?)d0["fieldName"]);
        Assert.Equal(0.5f, d0["lengthSeconds"]!.GetValue<float>(), 5);

        var channels = d0["channels"]!.AsArray();
        Assert.NotEmpty(channels);
        var totalKf = channels.Sum(c => c!["keyframes"]!.AsArray().Count);
        Assert.True(totalKf >= 4);

        var rot = channels.Select(c => c!.AsObject()).First(c => (string?)c["target"] == "ROTATION");
        Assert.Equal("LINEAR", rot["interpolation"]!.GetValue<string>());
        Assert.All(
            rot["keyframes"]!.AsArray().Select(k => k!.AsObject()),
            k => Assert.Equal("degrees", k["vectorKind"]!.GetValue<string>()));

        var pos = channels.Select(c => c!.AsObject()).First(c => (string?)c["target"] == "POSITION");
        Assert.Equal("LINEAR", pos["interpolation"]!.GetValue<string>());
        Assert.All(
            pos["keyframes"]!.AsArray().Select(k => k!.AsObject()),
            k => Assert.Equal("position", k["vectorKind"]!.GetValue<string>()));

        var posKfs = pos["keyframes"]!.AsArray().Select(k => k!.AsObject()).ToList();
        Assert.Contains(posKfs, k => Math.Abs(k["y"]!.GetValue<double>() - 5.0) < 0.01);
    }
}
