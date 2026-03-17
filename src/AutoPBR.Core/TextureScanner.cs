using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static class TextureScanner
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

    private static bool IsPathUnderPlantOrPlants(string relativePathNoExt)
    {
        return relativePathNoExt.Contains("\\plant\\", StringComparison.OrdinalIgnoreCase)
               || relativePathNoExt.Contains("\\plants\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlantForNoHeight(string relativePathNoExt, string foliageMode)
    {
        if (foliageMode != "No Height")
        {
            return false;
        }

        return AutoPbrDefaults.PlantTextureKeys.Contains(relativePathNoExt)
               || IsPathUnderPlantOrPlants(relativePathNoExt);
    }

    public static IReadOnlyList<TextureWorkItem> ScanTextures(string extractedPackRoot, AutoPbrOptions options)
    {
        var results = new List<TextureWorkItem>();

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
                        var fileName = Path.GetFileName(file);
                        if (AutoPbrDefaults.ExcludedFileNames.Contains(fileName))
                        {
                            continue;
                        }

                        if (fileName.Contains("sapling", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (fileName.Contains("mcmeta", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var name = Path.GetFileNameWithoutExtension(file);
                        if (name.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("_e", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(file);
                        var directoryPath = Path.GetDirectoryName(file) ?? dir;

                        var relativeToTextures = Path.GetRelativePath(
                            texturesRoot,
                            Path.Combine(directoryPath, name)
                        ).Replace('/', '\\');
                        var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToTextures;

                        if (options.IgnoreTextureKeys.Contains(relativePathNoExt))
                        {
                            continue;
                        }

                        if (options.FoliageMode == "Ignore All" && IsPathUnderPlantOrPlants(relativePathNoExt))
                        {
                            continue;
                        }

                        results.Add(new TextureWorkItem
                        {
                            FullPath = file,
                            DirectoryPath = directoryPath,
                            Name = name,
                            Extension = ext,
                            RelativeKey = relativePathNoExt,
                            SpecularOnly = specularOnly,
                            IsPlantForNoHeight = IsPlantForNoHeight(relativePathNoExt, options.FoliageMode)
                        });
                    }
                }
            }

            // OptiFine CTM
            var ctmRoot = Path.Combine(extractedPackRoot, "assets", namespaceName, "optifine", "ctm");
            if (Directory.Exists(ctmRoot))
            {
                foreach (var file in Directory.EnumerateFiles(ctmRoot, "*.png", SearchOption.AllDirectories))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Contains("mcmeta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("_e", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(file);
                    var directoryPath = Path.GetDirectoryName(file) ?? ctmRoot;

                    var relativeToNamespace = Path.GetRelativePath(
                        Path.Combine(extractedPackRoot, "assets", namespaceName),
                        Path.Combine(directoryPath, name)
                    ).Replace('/', '\\');
                    var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToNamespace;

                    if (options.IgnoreTextureKeys.Contains(relativePathNoExt))
                    {
                        continue;
                    }

                    results.Add(new TextureWorkItem
                    {
                        FullPath = file,
                        DirectoryPath = directoryPath,
                        Name = name,
                        Extension = ext,
                        RelativeKey = relativePathNoExt,
                        SpecularOnly = false,
                        IsPlantForNoHeight = false
                    });
                }
            }

            // OptiFine plant/plants
            if (options.FoliageMode != "Ignore All")
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
                        var fileName = Path.GetFileName(file);
                        if (fileName.Contains("mcmeta", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var name = Path.GetFileNameWithoutExtension(file);
                        if (name.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("_e", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(file);
                        var directoryPath = Path.GetDirectoryName(file) ?? plantRoot;
                        var relativeToNamespace = Path.GetRelativePath(
                            Path.Combine(extractedPackRoot, "assets", namespaceName),
                            Path.Combine(directoryPath, name)
                        ).Replace('/', '\\');
                        var relativePathNoExt = "\\" + namespaceName + "\\" + relativeToNamespace;
                        if (options.IgnoreTextureKeys.Contains(relativePathNoExt))
                        {
                            continue;
                        }

                        results.Add(new TextureWorkItem
                        {
                            FullPath = file,
                            DirectoryPath = directoryPath,
                            Name = name,
                            Extension = ext,
                            RelativeKey = relativePathNoExt,
                            SpecularOnly = false,
                            IsPlantForNoHeight = options.FoliageMode == "No Height"
                        });
                    }
                }
            }
        }

        return results;
    }
}

