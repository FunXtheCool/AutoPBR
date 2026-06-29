namespace AutoPBR.Core.Preview;

internal static class BlockPreviewBlockstateDefaults
{
    internal static string ResolveFamilyStem(string textureStem)
    {
        var stem = textureStem.ToLowerInvariant();
        foreach (var suf in new[] { "_side_overlay", "_lower", "_upper", "_bottom", "_top", "_inner", "_side", "_overlay" })
        {
            if (stem.EndsWith(suf, StringComparison.Ordinal) && stem.Length > suf.Length)
            {
                stem = stem[..^suf.Length];
                break;
            }
        }

        return stem;
    }

    internal static bool TryPickPreferredVariantKey(
        IReadOnlyList<string> variantKeys,
        string familyStem,
        string? textureStem,
        out string? bestKey)
    {
        bestKey = null;
        if (variantKeys.Count == 0)
        {
            return false;
        }

        string[] preferredPatterns = familyStem switch
        {
            _ when familyStem.EndsWith("_door", StringComparison.Ordinal) =>
                ["half=lower,facing=north", "half=lower", "facing=north"],
            _ when familyStem.Contains("trapdoor", StringComparison.Ordinal) =>
                ["facing=north,half=bottom,open=false", "half=bottom,open=false", "facing=north,half=bottom", "half=bottom"],
            "cake" when textureStem?.EndsWith("_inner", StringComparison.OrdinalIgnoreCase) == true =>
                ["bites=1"],
            "cake" => ["bites=0"],
            _ when familyStem.EndsWith("_stairs", StringComparison.Ordinal) =>
                ["facing=north,half=bottom", "half=bottom", "facing=north"],
            _ when familyStem.EndsWith("_fence", StringComparison.Ordinal) ||
                   familyStem.EndsWith("_fence_gate", StringComparison.Ordinal) =>
                ["north=false,east=false,south=false,west=false", "facing=north"],
            _ => [],
        };

        foreach (var pattern in preferredPatterns)
        {
            var exact = variantKeys.FirstOrDefault(k =>
                string.Equals(k, pattern, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                bestKey = exact;
                return true;
            }

            var partial = variantKeys.FirstOrDefault(k =>
                k.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            if (partial is not null)
            {
                bestKey = partial;
                return true;
            }
        }

        var lowerHalf = variantKeys.FirstOrDefault(k =>
            k.Contains("half=lower", StringComparison.OrdinalIgnoreCase) &&
            !k.Contains("half=upper", StringComparison.OrdinalIgnoreCase));
        if (lowerHalf is not null)
        {
            bestKey = lowerHalf;
            return true;
        }

        bestKey = variantKeys[0];
        return true;
    }

    internal static bool ShouldCollectAllMultipartApplies(string familyStem) =>
        familyStem.EndsWith("_stairs", StringComparison.Ordinal) ||
        familyStem.EndsWith("_fence", StringComparison.Ordinal) ||
        familyStem.EndsWith("_fence_gate", StringComparison.Ordinal) ||
        familyStem.EndsWith("_wall", StringComparison.Ordinal);
}
