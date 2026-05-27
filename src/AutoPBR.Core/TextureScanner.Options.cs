using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    private static IEnumerable<(string folder, bool specularOnly)> GetEnabledFolders(AutoPbrOptions options)
    {
        if (options.ProcessBlocks)
        {
            yield return ("blocks", false);
            yield return ("block", false);
        }

        if (options.ProcessItems)
        {
            yield return ("items", false);
            yield return ("item", false);
        }

        if (options.ProcessArmor)
        {
            yield return ("entity", false);
        }

        if (options.ProcessParticles)
        {
            yield return ("particle", true);
        }
    }

    private static IEnumerable<string> GetAssetNamespaces(string extractedPackRoot)
    {
        var assetsDir = Path.Combine(extractedPackRoot, "assets");
        if (!Directory.Exists(assetsDir))
        {
            yield break;
        }

        foreach (var dir in Directory.EnumerateDirectories(assetsDir))
        {
            yield return Path.GetFileName(dir);
        }
    }

    private static int ResolveMaxParallelism(AutoPbrOptions options)
    {
        if (options.MaxThreads > 0)
        {
            return Math.Max(1, options.MaxThreads);
        }

        return Math.Max(1, Environment.ProcessorCount - 2);
    }

    private static IReadOnlyList<TagRule> ResolveTagRules(AutoPbrOptions options) =>
        options.TagRules is { Count: > 0 } rules ? rules : TagRulePresets.Default;
}
