using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    private static TextureWorkItem BuildWorkItem(
        ScanCandidate candidate,
        TagComputationResult tags,
        AutoPbrOptions options,
        int? packBaseTileSize)
    {
        return new TextureWorkItem
        {
            FullPath = candidate.File,
            DirectoryPath = candidate.DirectoryPath,
            Name = candidate.Name,
            Extension = candidate.Extension,
            RelativeKey = candidate.RelativePathNoExt,
            SpecularOnly = candidate.SpecularOnly,
            IsPlantForNoHeight = FoliageModeResolver.IsNoHeight(options.FoliageMode) && tags.Sprite2DFoliageTarget,
            Sprite2DFoliageTarget = tags.Sprite2DFoliageTarget,
            HasPlantMaterialTag = tags.HasPlantMaterialTag,
            HasBrickMaterialTag = tags.HasBrickMaterialTag,
            HasUvWrap = tags.HasUvWrap,
            PackBaseTileSize = packBaseTileSize,
            Overrides =
            {
                InvertSpecular = tags.InvertSpecular,
                InvertHeight = tags.InvertHeight
            }
        };
    }

    private static bool IsSkippableByFilename(string file)
    {
        var fileName = Path.GetFileName(file);
        if (AutoPbrDefaults.ExcludedFileNames.Contains(fileName))
        {
            return true;
        }

        if (fileName.Contains("sapling", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.Contains("mcmeta", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var name = Path.GetFileNameWithoutExtension(file);
        return name.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_e", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredByKey(ScanCandidate candidate, AutoPbrOptions options) =>
        options.IgnoreTextureKeys.Contains(candidate.RelativePathNoExt);

    private static int? EstimatePackBaseTileSize(IEnumerable<ScanCandidate> candidates)
    {
        var histogram = new Dictionary<int, int>();
        foreach (var candidate in candidates)
        {
            if (candidate.SpecularOnly)
            {
                continue;
            }

            try
            {
                var info = Image.Identify(candidate.File);
                if (info.Width <= 0 || info.Height <= 0 || info.Width != info.Height)
                {
                    continue;
                }

                var edge = info.Width;
                if ((edge & (edge - 1)) != 0)
                {
                    continue;
                }

                histogram.TryGetValue(edge, out var count);
                histogram[edge] = count + 1;
            }
            catch
            {
                // Ignore unreadable textures while estimating baseline tile size.
            }
        }

        if (histogram.Count == 0)
        {
            return null;
        }

        return histogram
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .First()
            .Key;
    }

    private static IEnumerable<ScanCandidate> EnumerateScanCandidates(string extractedPackRoot, AutoPbrOptions options)
    {
        foreach (var namespaceName in GetAssetNamespaces(extractedPackRoot))
        {
            var texturesRoot = Path.Combine(extractedPackRoot, "assets", namespaceName, "textures");
            if (Directory.Exists(texturesRoot))
            {
                foreach (var (folder, specularOnly) in GetEnabledFolders(options))
                {
                    var dir = Path.Combine(texturesRoot, folder);
                    if (!Directory.Exists(dir))
                    {
                        continue;
                    }

                    foreach (var file in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var ext = Path.GetExtension(file);
                        var directoryPath = Path.GetDirectoryName(file) ?? dir;
                        var relativeToTextures = Path.GetRelativePath(
                            texturesRoot,
                            Path.Combine(directoryPath, name)).Replace('/', '\\');
                        var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToTextures;
                        yield return new ScanCandidate(file, name, ext, directoryPath, relativePathNoExt, specularOnly);
                    }
                }
            }

            var ctmRoot = Path.Combine(extractedPackRoot, "assets", namespaceName, "optifine", "ctm");
            if (Directory.Exists(ctmRoot))
            {
                foreach (var file in Directory.EnumerateFiles(ctmRoot, "*.png", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var ext = Path.GetExtension(file);
                    var directoryPath = Path.GetDirectoryName(file) ?? ctmRoot;
                    var relativeToNamespace = Path.GetRelativePath(
                        Path.Combine(extractedPackRoot, "assets", namespaceName),
                        Path.Combine(directoryPath, name)).Replace('/', '\\');
                    var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToNamespace;
                    yield return new ScanCandidate(file, name, ext, directoryPath, relativePathNoExt, false);
                }
            }

            if (!FoliageModeResolver.IsIgnoreAll(options.FoliageMode))
            {
                foreach (var plantFolder in new[] { "plant", "plants" })
                {
                    var plantRoot = Path.Combine(extractedPackRoot, "assets", namespaceName, "optifine", plantFolder);
                    if (!Directory.Exists(plantRoot))
                    {
                        continue;
                    }

                    foreach (var file in Directory.EnumerateFiles(plantRoot, "*.png", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var ext = Path.GetExtension(file);
                        var directoryPath = Path.GetDirectoryName(file) ?? plantRoot;
                        var relativeToNamespace = Path.GetRelativePath(
                            Path.Combine(extractedPackRoot, "assets", namespaceName),
                            Path.Combine(directoryPath, name)).Replace('/', '\\');
                        var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToNamespace;
                        yield return new ScanCandidate(file, name, ext, directoryPath, relativePathNoExt, false);
                    }
                }
            }
        }
    }
}
