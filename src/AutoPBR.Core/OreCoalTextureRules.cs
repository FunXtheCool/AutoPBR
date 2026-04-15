namespace AutoPBR.Core;

/// <summary>
/// Path-based tuning for coal ore textures (same token boundaries as Explore tag rules).
/// </summary>
internal static class OreCoalTextureRules
{
    /// <summary>
    /// When both whole-word <c>ore</c> and <c>coal</c> appear in the texture title or path below namespace
    /// (e.g. <c>coal_ore</c>, <c>deepslate_coal_ore</c>), height derived from diffuse should be inverted for LabPBR.
    /// </summary>
    internal static bool ShouldInvertHeight(string textureName, string relativeKey)
    {
        var pathBelow = TagRuleApplicator.PathBelowNamespace(relativeKey);
        var combined = textureName + "\0" + pathBelow;
        return TagRuleApplicator.KeywordMatches(combined, "ore", wholeWord: true)
            && TagRuleApplicator.KeywordMatches(combined, "coal", wholeWord: true);
    }
}
