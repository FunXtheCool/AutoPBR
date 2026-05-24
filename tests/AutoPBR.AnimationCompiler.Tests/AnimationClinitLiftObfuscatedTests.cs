using AutoPBR.Tools.AnimationCompiler;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class AnimationClinitLiftObfuscatedTests
{
    private static string MappingsPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");

    private static string BatObfuscatedJavapPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            "generated",
            "minecraft-client-model-index-1.21.11-animation-init",
            "net_minecraft_client_animation_definitions_BatAnimation.javapc.txt");

    [Fact]
    public void TryLift_BatAnimation_obfuscated_javap_reaches_ok_when_mappings_normalize()
    {
        if (!File.Exists(MappingsPath) || !File.Exists(BatObfuscatedJavapPath))
        {
            return;
        }

        var maps = MojangMappingsParser.Load(MappingsPath);
        var javapOut = File.ReadAllText(BatObfuscatedJavapPath);
        var normalized = AnimationJavapObfuscationNormalizer.Normalize(
            javapOut,
            "net.minecraft.client.animation.definitions.BatAnimation",
            maps);
        Assert.True(AnimationClinitLift.TryLift(normalized, out var defs, out var notes),
            string.Join("; ", notes));
        Assert.Equal(2, defs.Count);
        Assert.Equal("BAT_RESTING", (string?)defs[0]!["fieldName"]);
        Assert.Equal("BAT_FLYING", (string?)defs[1]!["fieldName"]);
        Assert.All(defs, d => Assert.True(d!["channels"]!.AsArray().Count > 0));
        Assert.False(AnimationClinitLift.HasIncompleteChannels(defs));
    }

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "AutoPBR.sln")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
