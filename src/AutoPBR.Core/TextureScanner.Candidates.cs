using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    private static bool ShouldRunSemanticMlForCandidate(
        ScanCandidate candidate,
        AutoPbrOptions options,
        IReadOnlyList<TagRule> rules)
    {
        if (IsNumericOnlyOptifineTile(candidate.Name, candidate.RelativePathNoExt))
        {
            return false;
        }

        var sem = options.SemanticOptions;
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return false;
        }

        var heuristicMaterialIds = TagRuleApplicator.GetMatchingTagIds(
            candidate.Name,
            candidate.RelativePathNoExt,
            rules,
            TagRuleKind.Material);
        if (heuristicMaterialIds.Count > 0)
        {
            return false;
        }

        if (options.ManualTagOverrides is not null &&
            options.ManualTagOverrides.TryGetValue(candidate.RelativePathNoExt, out var overrides))
        {
            var materialRuleIds = new HashSet<string>(
                rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
                StringComparer.OrdinalIgnoreCase);
            foreach (var id in overrides.Added)
            {
                if (materialRuleIds.Contains(id))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsNumericOnlyOptifineTile(string textureName, string relativePathNoExt)
    {
        if (!IsOptifinePath(relativePathNoExt))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(textureName))
        {
            return false;
        }

        foreach (var c in textureName)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOptifinePath(string relativePathNoExt) =>
        relativePathNoExt.Contains("\\optifine\\", StringComparison.OrdinalIgnoreCase);

    private static string? GetParentRelativePathNoExt(string relativePathNoExt)
    {
        var lastSlash = relativePathNoExt.LastIndexOf('\\');
        if (lastSlash <= 0)
        {
            return null;
        }

        return relativePathNoExt[..lastSlash];
    }

    private static string GetLeafSegment(string pathNoExt)
    {
        var lastSlash = pathNoExt.LastIndexOf('\\');
        if (lastSlash < 0 || lastSlash == pathNoExt.Length - 1)
        {
            return pathNoExt;
        }

        return pathNoExt[(lastSlash + 1)..];
    }
}
