namespace AutoPBR.Core.Preview;

internal sealed record MinecraftNativeProfile(string Name, string RootDirectory, Version? ParsedVersion);

internal static class MinecraftNativeProfileResolver
{
    /// <summary>
    /// Resolve native minecraft data root under Data/minecraft-native.
    /// Auto mode prefers the highest parsable version folder (e.g. 26.1.2 > 1.21.11), then lexical fallback.
    /// </summary>
    public static MinecraftNativeProfile? ResolveAutoLatest(string nativeRoot)
    {
        if (!Directory.Exists(nativeRoot))
        {
            return null;
        }

        var candidates = EnumerateProfiles(nativeRoot).ToList();
        if (candidates.Count == 0)
        {
            return new MinecraftNativeProfile("root", nativeRoot, null);
        }

        return SelectHighestVersion(candidates);
    }

    /// <summary>
    /// Highest native profile strictly newer than the 1.21.11 legacy ceiling (never returns 1.21.11).
    /// </summary>
    public static MinecraftNativeProfile? ResolveAutoLatestModern(string nativeRoot)
    {
        if (!Directory.Exists(nativeRoot))
        {
            return null;
        }

        var modern = EnumerateProfiles(nativeRoot)
            .Where(IsModernProfile)
            .ToList();
        return modern.Count == 0 ? null : SelectHighestVersion(modern);
    }

    /// <summary>
    /// Picks 1.21.11 native data only when the input path or pack.mcmeta indicates game version &lt;= 1.21.11;
    /// otherwise prefers modern (&gt; 1.21.11) folders.
    /// </summary>
    public static MinecraftNativeProfile? ResolveForPreview(
        string nativeRoot,
        string? inputZipPath = null,
        string? extractedPackDir = null)
    {
        if (!Directory.Exists(nativeRoot))
        {
            return null;
        }

        if (MinecraftPreviewVersionDetection.TryDetect(inputZipPath, extractedPackDir, out var detected) &&
            MinecraftPreviewVersionGate.IsLegacyGameVersion(detected))
        {
            return TryResolveByLabel(nativeRoot, MinecraftPreviewVersionGate.LegacyNativeProfileLabel)
                   ?? ResolveAutoLatest(nativeRoot);
        }

        return ResolveAutoLatestModern(nativeRoot) ?? ResolveAutoLatest(nativeRoot);
    }

    public static MinecraftNativeProfile? TryResolveByLabel(string nativeRoot, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var dir = Path.Combine(nativeRoot, label);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        return new MinecraftNativeProfile(label, dir, TryParseVersionLike(label));
    }

    private static bool IsModernProfile(MinecraftNativeProfile profile)
    {
        if (profile.ParsedVersion is { } v)
        {
            return MinecraftPreviewVersionGate.IsModernGameVersion(v);
        }

        return !string.Equals(
            profile.Name,
            MinecraftPreviewVersionGate.LegacyNativeProfileLabel,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Versioned native asset roots only (e.g. <c>1.21.11</c>, <c>26.1.2</c>).
    /// Skips IR payload folders (<c>geometry</c>, <c>animation</c>, <c>setup-anim</c>, …) deployed beside them.
    /// </summary>
    private static IEnumerable<MinecraftNativeProfile> EnumerateProfiles(string nativeRoot) =>
        Directory.EnumerateDirectories(nativeRoot)
            .Select(d => Path.GetFileName(d))
            .Select(name => (Name: name, Version: TryParseVersionLike(name), Dir: Path.Combine(nativeRoot, name!)))
            .Where(t => t.Version is not null && !string.IsNullOrEmpty(t.Name))
            .Select(t => new MinecraftNativeProfile(t.Name!, t.Dir, t.Version));

    private static MinecraftNativeProfile SelectHighestVersion(IReadOnlyList<MinecraftNativeProfile> candidates) =>
        candidates
            .OrderByDescending(c => c.ParsedVersion is not null)
            .ThenByDescending(c => c.ParsedVersion)
            .ThenByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .First();

    internal static Version? TryParseVersionLike(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 4)
        {
            return null;
        }

        var nums = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0)
            {
                return null;
            }
        }

        return parts.Length switch
        {
            2 => new Version(nums[0], nums[1]),
            3 => new Version(nums[0], nums[1], nums[2]),
            _ => new Version(nums[0], nums[1], nums[2], nums[3])
        };
    }
}
