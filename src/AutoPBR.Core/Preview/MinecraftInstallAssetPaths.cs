namespace AutoPBR.Core.Preview;

internal static class MinecraftInstallAssetPaths
{
    /// <summary>
    /// Resolves a user-provided directory to an <c>assets/</c> root suitable for <see cref="DirectoryAssetSource"/>.
    /// Accepts the install root, a version folder, or a path that already ends in <c>assets</c>.
    /// </summary>
    public static bool TryResolveAssetsRoot(string? configuredPath, out string assetsRoot)
    {
        assetsRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var path = Path.GetFullPath(configuredPath.Trim());
        if (!Directory.Exists(path))
        {
            return false;
        }

        if (Path.GetFileName(path).Equals("assets", StringComparison.OrdinalIgnoreCase))
        {
            assetsRoot = path;
            return HasBlockModels(assetsRoot);
        }

        var directAssets = Path.Combine(path, "assets");
        if (Directory.Exists(directAssets) && HasBlockModels(directAssets))
        {
            assetsRoot = directAssets;
            return true;
        }

        foreach (var versionAssets in Directory.EnumerateDirectories(path, "assets", SearchOption.AllDirectories))
        {
            if (HasBlockModels(versionAssets))
            {
                assetsRoot = versionAssets;
                return true;
            }
        }

        return false;
    }

    private static bool HasBlockModels(string assetsRoot)
    {
        var probe = Path.Combine(assetsRoot, "minecraft", "models", "block");
        return Directory.Exists(probe);
    }
}
