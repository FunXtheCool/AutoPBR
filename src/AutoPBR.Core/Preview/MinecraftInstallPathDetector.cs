namespace AutoPBR.Core.Preview;

public static class MinecraftInstallPathDetector
{
    public static string? TryDetectDefaultAssetsRoot()
    {
        foreach (var gameRoot in EnumerateCandidateGameRoots())
        {
            var versionsDir = Path.Combine(gameRoot, "versions");
            if (!Directory.Exists(versionsDir))
            {
                continue;
            }

            var versionFolder = Directory.EnumerateDirectories(versionsDir)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (versionFolder is null)
            {
                continue;
            }

            var versionRoot = Path.Combine(versionsDir, versionFolder);
            if (MinecraftInstallAssetPaths.TryResolveAssetsRoot(versionRoot, out var assetsRoot))
            {
                return assetsRoot;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateGameRoots()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, ".minecraft");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".minecraft");
            if (OperatingSystem.IsMacOS())
            {
                yield return Path.Combine(home, "Library", "Application Support", "minecraft");
            }
        }
    }
}
