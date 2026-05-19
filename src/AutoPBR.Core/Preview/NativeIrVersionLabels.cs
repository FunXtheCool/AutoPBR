namespace AutoPBR.Core.Preview;

/// <summary>
/// Resolves IR shard version labels for a native profile without crossing legacy/modern boundaries.
/// </summary>
internal static class NativeIrVersionLabels
{
    public const string ModernGeometryLabel = "26.1.2";

    public static bool IsRecognizedProfileName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        !string.Equals(name, "root", StringComparison.Ordinal) &&
        !string.Equals(name, "unknown", StringComparison.OrdinalIgnoreCase) &&
        MinecraftNativeProfileResolver.TryParseVersionLike(name) is not null;

    public static IEnumerable<string> ForProfile(MinecraftNativeProfile? profile)
    {
        if (profile is { Name: var n } && IsRecognizedProfileName(n))
        {
            yield return n;
            yield break;
        }

        yield return ModernGeometryLabel;
    }

    /// <summary>
    /// Geometry IR lookup order. Legacy packs try 1.21.11 shards first, then modern lifted shards for the same JVM.
    /// Animation/setupAnim and other assets still use <see cref="ForProfile"/> only.
    /// </summary>
    public static IEnumerable<string> ForGeometryIrShardLookup(MinecraftNativeProfile? profile)
    {
        if (profile is { Name: var n } && IsRecognizedProfileName(n))
        {
            yield return n;
            if (string.Equals(n, MinecraftPreviewVersionGate.LegacyNativeProfileLabel, StringComparison.Ordinal) &&
                !string.Equals(n, ModernGeometryLabel, StringComparison.Ordinal))
            {
                yield return ModernGeometryLabel;
            }

            yield break;
        }

        yield return ModernGeometryLabel;
    }

    public static string? PrimaryForProfile(MinecraftNativeProfile? profile)
    {
        foreach (var label in ForProfile(profile))
        {
            return label;
        }

        return null;
    }
}
