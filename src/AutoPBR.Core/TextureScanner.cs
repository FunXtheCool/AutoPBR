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

    /// <summary>True when effective tags include <see cref="FlagTagResolver.Sprite2DId"/> (same rules as Explore).</summary>
    private static bool GetSprite2DFoliageTarget(AutoPbrOptions options, string textureName, string relativePathNoExt)
    {
        var rules = options.TagRules ?? TagRulePresets.Default;
        var sem = options.SemanticOptions;
        IReadOnlyCollection<string>? added = null;
        IReadOnlyCollection<string>? removed = null;
        if (options.ManualTagOverrides?.TryGetValue(relativePathNoExt, out var o) == true)
        {
            added = o.Added;
            removed = o.Removed;
        }

        var includeDict = sem?.DictionaryEvidenceEnabled ?? false;
        var effective = ConversionEffectiveTags.ComputeEffectiveTagIds(
            textureName,
            relativePathNoExt,
            rules,
            sem,
            includeDict,
            deferSemanticMl: false,
            added,
            removed);
        return effective.Contains(FlagTagResolver.Sprite2DId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Effective material tag ids (keywords / ML / post-processor / explorer manual add-remove) for a texture key.
    /// </summary>
    private static HashSet<string> GetEffectiveMaterialTagIds(string relativePathNoExt, AutoPbrOptions options)
    {
        var rules = options.TagRules ?? TagRulePresets.Default;
        var name = Path.GetFileNameWithoutExtension(relativePathNoExt.Replace('\\', '/'));

        var sem = options.SemanticOptions;
        var autoMaterialIds = MaterialTagSemanticResolution.ResolveMaterialTags(
            name,
            relativePathNoExt,
            rules,
            sem,
            deferSemanticMl: false,
            sem?.DictionaryEvidenceEnabled ?? false,
            out _);

        var effective = new HashSet<string>(autoMaterialIds, StringComparer.OrdinalIgnoreCase);
        if (options.ManualTagOverrides is not null &&
            options.ManualTagOverrides.TryGetValue(relativePathNoExt, out var overrides))
        {
            foreach (var removed in overrides.Removed)
            {
                effective.Remove(removed);
            }

            foreach (var added in overrides.Added)
            {
                effective.Add(added);
            }
        }

        return effective;
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

                        var effectiveMaterialIds = GetEffectiveMaterialTagIds(relativePathNoExt, options);
                        var sprite2DFoliage = GetSprite2DFoliageTarget(options, name, relativePathNoExt);

                        if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && sprite2DFoliage)
                        {
                            continue;
                        }

                        var blockItem = new TextureWorkItem
                        {
                            FullPath = file,
                            DirectoryPath = directoryPath,
                            Name = name,
                            Extension = ext,
                            RelativeKey = relativePathNoExt,
                            SpecularOnly = specularOnly,
                            IsPlantForNoHeight = FoliageModeResolver.IsNoHeight(options.FoliageMode) && sprite2DFoliage,
                            Sprite2DFoliageTarget = sprite2DFoliage,
                            HasPlantMaterialTag = effectiveMaterialIds.Contains("plant")
                        };
                        blockItem.Overrides.InvertSpecular = effectiveMaterialIds.Contains("brick");
                        blockItem.Overrides.InvertHeight = OreCoalTextureRules.ShouldInvertHeight(name, relativePathNoExt);
                        results.Add(blockItem);
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

                    var ctmEffectiveMaterialIds = GetEffectiveMaterialTagIds(relativePathNoExt, options);
                    var ctmSprite2DFoliage = GetSprite2DFoliageTarget(options, name, relativePathNoExt);
                    if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && ctmSprite2DFoliage)
                    {
                        continue;
                    }

                    var ctmItem = new TextureWorkItem
                    {
                        FullPath = file,
                        DirectoryPath = directoryPath,
                        Name = name,
                        Extension = ext,
                        RelativeKey = relativePathNoExt,
                        SpecularOnly = false,
                        IsPlantForNoHeight = FoliageModeResolver.IsNoHeight(options.FoliageMode) && ctmSprite2DFoliage,
                        Sprite2DFoliageTarget = ctmSprite2DFoliage,
                        HasPlantMaterialTag = ctmEffectiveMaterialIds.Contains("plant")
                    };
                    ctmItem.Overrides.InvertSpecular = ctmEffectiveMaterialIds.Contains("brick");
                    ctmItem.Overrides.InvertHeight = OreCoalTextureRules.ShouldInvertHeight(name, relativePathNoExt);
                    results.Add(ctmItem);
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

                        var plantFolderSprite2DFoliage = GetSprite2DFoliageTarget(options, name, relativePathNoExt);
                        if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && plantFolderSprite2DFoliage)
                        {
                            continue;
                        }

                        var plantFolderMaterialIds = GetEffectiveMaterialTagIds(relativePathNoExt, options);
                        var plantFolderItem = new TextureWorkItem
                        {
                            FullPath = file,
                            DirectoryPath = directoryPath,
                            Name = name,
                            Extension = ext,
                            RelativeKey = relativePathNoExt,
                            SpecularOnly = false,
                            IsPlantForNoHeight = FoliageModeResolver.IsNoHeight(options.FoliageMode) && plantFolderSprite2DFoliage,
                            Sprite2DFoliageTarget = plantFolderSprite2DFoliage,
                            HasPlantMaterialTag = true
                        };
                        plantFolderItem.Overrides.InvertSpecular = plantFolderMaterialIds.Contains("brick");
                        plantFolderItem.Overrides.InvertHeight = OreCoalTextureRules.ShouldInvertHeight(name, relativePathNoExt);
                        results.Add(plantFolderItem);
                    }
                }
            }
        }

        return results;
    }
}

