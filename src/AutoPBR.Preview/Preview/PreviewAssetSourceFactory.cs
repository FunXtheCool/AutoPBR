namespace AutoPBR.Preview;

internal enum PreviewModelJsonOrigin
{
    Pack = 0,
    MinecraftInstall,
    NativeBundled,
}

internal sealed class PreviewAssetSources
{
    public required IAssetSource Composite { get; init; }

    public IAssetSource? PackSource { get; init; }

    public IAssetSource? InstallSource { get; init; }

    public IAssetSource? NativeSource { get; init; }

    public PreviewModelJsonOrigin ResolveModelJsonOrigin(string modelJsonZipPath)
    {
        if (PackSource?.Exists(modelJsonZipPath) == true)
        {
            return PreviewModelJsonOrigin.Pack;
        }

        if (InstallSource?.Exists(modelJsonZipPath) == true)
        {
            return PreviewModelJsonOrigin.MinecraftInstall;
        }

        if (NativeSource?.Exists(modelJsonZipPath) == true)
        {
            return PreviewModelJsonOrigin.NativeBundled;
        }

        return PreviewModelJsonOrigin.Pack;
    }

    public static string FormatModelJsonDetail(string modelJsonZipPath, PreviewModelJsonOrigin origin) =>
        origin switch
        {
            PreviewModelJsonOrigin.MinecraftInstall => $"{modelJsonZipPath} · install",
            PreviewModelJsonOrigin.NativeBundled => $"{modelJsonZipPath} · native",
            _ => modelJsonZipPath,
        };
}

internal static class PreviewAssetSourceFactory
{
    /// <summary>Priority: pack zip, optional Minecraft install assets, bundled native catalogs.</summary>
    public static PreviewAssetSources Create(
        IAssetSource packSource,
        string? minecraftAssetsDirectory,
        string? nativeRootDirectory)
    {
        IAssetSource? installSource = null;
        if (MinecraftInstallAssetPaths.TryResolveAssetsRoot(minecraftAssetsDirectory, out var installRoot))
        {
            installSource = new DirectoryAssetSource(installRoot);
        }

        IAssetSource? nativeSource = null;
        if (!string.IsNullOrWhiteSpace(nativeRootDirectory) && Directory.Exists(nativeRootDirectory))
        {
            nativeSource = new DirectoryAssetSource(nativeRootDirectory);
        }

        var sources = new List<IAssetSource> { packSource };
        if (installSource is not null)
        {
            sources.Add(installSource);
        }

        if (nativeSource is not null)
        {
            sources.Add(nativeSource);
        }

        return new PreviewAssetSources
        {
            Composite = sources.Count == 1 ? packSource : new CompositeAssetSource(sources.ToArray()),
            PackSource = packSource,
            InstallSource = installSource,
            NativeSource = nativeSource,
        };
    }
}
