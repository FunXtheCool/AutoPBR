
using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class AnimationClinitLiftRegressionTests
{
    private static string JavapcPath(string fileName) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            "generated",
            "minecraft-client-model-index-26.1.2-animation-init",
            fileName);

    [Fact]
    public void TryLift_Nautilus_swimming_body_scale_has_keyframes()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_NautilusAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        var swim = defs.Select(d => d!.AsObject()).First(d => (string?)d["fieldName"] == "SWIMMING");
        var bodyScale = swim["channels"]!.AsArray()
            .Select(c => c!.AsObject())
            .First(c => (string?)c["partName"] == "body" && (string?)c["target"] == "SCALE");
        var kf = bodyScale["keyframes"]!.AsArray();
        Assert.True(kf.Count >= 5, $"expected body SCALE keyframes, notes: {string.Join("; ", notes)}");
        Assert.Equal("LINEAR", bodyScale["interpolation"]!.GetValue<string>());
    }

    [Fact]
    public void TryLift_Sniffer_sniff_sniff_has_channels_with_keyframes()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_SnifferAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        var sniff = defs.Select(d => d!.AsObject()).First(d => (string?)d["fieldName"] == "SNIFFER_SNIFFSNIFF");
        var channels = sniff["channels"]!.AsArray();
        Assert.NotEmpty(channels);
        var totalKf = channels.Sum(c => c!["keyframes"]!.AsArray().Count);
        Assert.True(totalKf >= 9, $"expected nose/leg channels, got {totalKf} keyframes");
        Assert.Equal(8f, sniff["lengthSeconds"]!.GetValue<float>(), 3);
    }

    [Fact]
    public void TryLift_BabyAxolotl_reduces_empty_scale_channels()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_BabyAxolotlAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        var empty = defs.SelectMany(d => d!["channels"]!.AsArray())
            .Count(c => c!["keyframes"]!.AsArray().Count == 0);
        Assert.True(empty < 8, $"too many empty channels ({empty}); notes: {string.Join("; ", notes.Take(6))}");
    }

    [Fact]
    public void TryLift_Warden_emerge_lifts_long_channel_part_names()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_WardenAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        Assert.DoesNotContain(notes, n => n.Contains("missing preceding ldc String", StringComparison.Ordinal));
        var emerge = defs.Select(d => d!.AsObject()).First(d => (string?)d["fieldName"] == "WARDEN_EMERGE");
        var channels = emerge["channels"]!.AsArray();
        Assert.NotEmpty(channels);
        Assert.False(AnimationClinitLift.HasIncompleteChannels(defs));
    }

    [Fact]
    public void TryLift_CopperGolem_chest_interaction_body_has_no_skipped_keyframes()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_CopperGolemAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        Assert.DoesNotContain(notes, n => n.Contains("skipped (unrecognized vec/time layout)", StringComparison.Ordinal));
        var drop = defs.Select(d => d!.AsObject()).First(d => (string?)d["fieldName"] == "COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP");
        var body = drop["channels"]!.AsArray()
            .Select(c => c!.AsObject())
            .First(c => (string?)c["partName"] == "body" && (string?)c["target"] == "ROTATION");
        Assert.True(body["keyframes"]!.AsArray().Count >= 25);
    }

    [Fact]
    public void TryLift_Armadillo_no_missing_part_name_notes()
    {
        var text = File.ReadAllText(JavapcPath("net_minecraft_client_animation_definitions_ArmadilloAnimation.javapc.txt"));
        Assert.True(AnimationClinitLift.TryLift(text, out var defs, out var notes), string.Join("; ", notes));
        Assert.DoesNotContain(notes, n => n.Contains("missing preceding ldc String", StringComparison.Ordinal));
        Assert.False(AnimationClinitLift.HasIncompleteChannels(defs));
    }
}
