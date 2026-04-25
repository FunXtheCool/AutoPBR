using System.Collections.Concurrent;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static class TextureScanner
{
    private sealed record ScanCandidate(
        string File,
        string Name,
        string Extension,
        string DirectoryPath,
        string RelativePathNoExt,
        bool SpecularOnly);

    private sealed record TagComputationResult(
        bool Sprite2DFoliageTarget,
        bool HasPlantMaterialTag,
        bool HasBrickMaterialTag,
        bool InvertSpecular,
        bool InvertHeight,
        IReadOnlyList<string> EffectiveTagIds);

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

    private static int ResolveMaxParallelism(AutoPbrOptions options)
    {
        if (options.MaxThreads > 0)
        {
            return Math.Max(1, options.MaxThreads);
        }

        return Math.Max(1, Environment.ProcessorCount - 2);
    }

    private static IReadOnlyList<TagRule> ResolveTagRules(AutoPbrOptions options) =>
        options.TagRules is { Count: > 0 } rules ? rules : TagRulePresets.Default;

    private static bool ShouldRunSemanticMlForCandidate(
        ScanCandidate candidate,
        AutoPbrOptions options,
        IReadOnlyList<TagRule> rules)
    {
        if (IsNumericOnlyOptifineTile(candidate.Name, candidate.RelativePathNoExt))
        {
            return false;
        }

        var sem = options.SemanticOptions;
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return false;
        }

        var heuristicMaterialIds = TagRuleApplicator.GetMatchingTagIds(
            candidate.Name,
            candidate.RelativePathNoExt,
            rules,
            TagRuleKind.Material);
        if (heuristicMaterialIds.Count > 0)
        {
            return false;
        }

        if (options.ManualTagOverrides is not null &&
            options.ManualTagOverrides.TryGetValue(candidate.RelativePathNoExt, out var overrides))
        {
            var materialRuleIds = new HashSet<string>(
                rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
                StringComparer.OrdinalIgnoreCase);
            foreach (var id in overrides.Added)
            {
                if (materialRuleIds.Contains(id))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsNumericOnlyOptifineTile(string textureName, string relativePathNoExt)
    {
        if (!IsOptifinePath(relativePathNoExt))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(textureName))
        {
            return false;
        }

        foreach (var c in textureName)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOptifinePath(string relativePathNoExt) =>
        relativePathNoExt.Contains("\\optifine\\", StringComparison.OrdinalIgnoreCase);

    private static string? GetParentRelativePathNoExt(string relativePathNoExt)
    {
        var lastSlash = relativePathNoExt.LastIndexOf('\\');
        if (lastSlash <= 0)
        {
            return null;
        }

        return relativePathNoExt[..lastSlash];
    }

    private static string GetLeafSegment(string pathNoExt)
    {
        var lastSlash = pathNoExt.LastIndexOf('\\');
        if (lastSlash < 0 || lastSlash == pathNoExt.Length - 1)
        {
            return pathNoExt;
        }

        return pathNoExt[(lastSlash + 1)..];
    }

    private static TagComputationResult BuildTagComputationResultFromEffectiveIds(
        ScanCandidate candidate,
        IReadOnlyCollection<string> effectiveIds,
        IReadOnlyDictionary<string, bool>? overrideDecisions = null)
    {
        var effective = new HashSet<string>(effectiveIds, StringComparer.OrdinalIgnoreCase);
        var invertSpecular = effective.Contains("brick");
        var invertHeight = OreCoalTextureRules.ShouldInvertHeight(candidate.Name, candidate.RelativePathNoExt);
        if (overrideDecisions is not null)
        {
            if (overrideDecisions.TryGetValue("invert_specular", out var invSpec))
            {
                invertSpecular = invSpec;
            }

            if (overrideDecisions.TryGetValue("invert_height", out var invHeight))
            {
                invertHeight = invHeight;
            }
        }

        return new TagComputationResult(
            effective.Contains(FlagTagResolver.Sprite2DId),
            effective.Contains("organic") ||
            effective.Contains("plant") ||
            AutoPbrDefaults.PlantTextureKeys.Contains(candidate.RelativePathNoExt),
            effective.Contains("brick"),
            invertSpecular,
            invertHeight,
            effective.ToList());
    }

    private static TagComputationResult ComputeTagsForCandidate(
        ScanCandidate candidate,
        AutoPbrOptions options,
        IReadOnlyList<TagRule> rules,
        bool deferSemanticMl,
        IReadOnlyCollection<string>? inheritedMaterialTagIds = null)
    {
        IReadOnlyCollection<string>? added = null;
        IReadOnlyCollection<string>? removed = null;
        if (options.ManualTagOverrides?.TryGetValue(candidate.RelativePathNoExt, out var o) == true)
        {
            added = o.Added;
            removed = o.Removed;
        }

        var sem = options.SemanticOptions;
        var includeDict = sem?.DictionaryEvidenceEnabled ?? false;
        var resolution = ConversionEffectiveTags.ComputeResolution(
            candidate.Name,
            candidate.RelativePathNoExt,
            candidate.File,
            rules,
            sem,
            includeDict,
            deferSemanticMl,
            added,
            removed);
        var effective = new HashSet<string>(resolution.EffectiveIds, StringComparer.OrdinalIgnoreCase);
        if (inheritedMaterialTagIds is { Count: > 0 })
        {
            HashSet<string>? removedSet = null;
            if (removed is { Count: > 0 })
            {
                removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var inheritedId in inheritedMaterialTagIds)
            {
                if (removedSet is not null && removedSet.Contains(inheritedId))
                {
                    continue;
                }

                effective.Add(inheritedId);
            }
        }

        return BuildTagComputationResultFromEffectiveIds(candidate, effective, resolution.OverrideDecisions);
    }

    private static Dictionary<string, IReadOnlyCollection<string>> BuildNumericOptifineFolderMaterialHints(
        IReadOnlyList<ScanCandidate> candidates,
        AutoPbrOptions options,
        IReadOnlyList<TagRule> rules,
        CancellationToken cancellationToken)
    {
        var hints = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        var sem = options.SemanticOptions;
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return hints;
        }

        var includeDict = sem.DictionaryEvidenceEnabled;
        var materialRuleIds = new HashSet<string>(
            rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
        if (materialRuleIds.Count == 0)
        {
            return hints;
        }

        var parentFolders = candidates
            .Where(static c => IsNumericOnlyOptifineTile(c.Name, c.RelativePathNoExt))
            .Select(static c => GetParentRelativePathNoExt(c.RelativePathNoExt))
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folderKey in parentFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderName = GetLeafSegment(folderKey);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            var heuristicMaterialIds = TagRuleApplicator.GetMatchingTagIds(
                folderName,
                folderKey,
                rules,
                TagRuleKind.Material);
            if (heuristicMaterialIds.Count > 0)
            {
                hints[folderKey] = heuristicMaterialIds
                    .Where(materialRuleIds.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                continue;
            }

            var inferredIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
                folderName,
                folderKey,
                texturePath: null,
                rules,
                sem,
                includeDict,
                deferSemanticMl: false,
                added: null,
                removed: null);
            var inferredMaterialIds = inferredIds
                .Where(materialRuleIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (inferredMaterialIds.Length > 0)
            {
                hints[folderKey] = inferredMaterialIds;
            }
        }

        return hints;
    }

    private static TextureWorkItem BuildWorkItem(
        ScanCandidate candidate,
        TagComputationResult tags,
        AutoPbrOptions options)
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

    public static IReadOnlyList<TextureWorkItem> ScanTextures(
        string extractedPackRoot,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress = null,
        string? cachePackPath = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TextureWorkItem>();
        var candidates = EnumerateScanCandidates(extractedPackRoot, options).ToList();
        var totalCandidates = Math.Max(1, candidates.Count);
        var processedCandidates = 0;
        var rules = ResolveTagRules(options);
        var tagCache = new ConcurrentDictionary<string, TagComputationResult>(StringComparer.OrdinalIgnoreCase);
        var includedCandidates = new List<ScanCandidate>(candidates.Count);
        var manualOverrides = options.ManualTagOverrides?.ToDictionary(
            static kv => kv.Key,
            static kv => (kv.Value.Added, kv.Value.Removed),
            StringComparer.OrdinalIgnoreCase);
        var signature = SharedEffectiveTagsCacheSignature.Compute(rules, options.SemanticOptions, manualOverrides);
        var persistedSnapshot = SharedEffectiveTagsCachePersistence.Load(cachePackPath ?? extractedPackRoot);
        var persistedEffectiveIdsByKey = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (persistedSnapshot is not null &&
            string.Equals(persistedSnapshot.Signature, signature, StringComparison.Ordinal))
        {
            foreach (var kv in persistedSnapshot.EffectiveTagIdsByStorageKey)
            {
                persistedEffectiveIdsByKey[kv.Key] = kv.Value;
            }
        }

        var noMlCandidates = new List<ScanCandidate>();
        var mlCandidates = new List<ScanCandidate>();
        var optifineCandidates = 0;
        var optifineNumericMlSkipped = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processed = Interlocked.Increment(ref processedCandidates);
            progress?.Report(new ConversionProgress(
                ConversionStage.ScanningTextures,
                processed,
                totalCandidates,
                Path.GetFileName(candidate.File)));

            if (IsSkippableByFilename(candidate.File) || IsIgnoredByKey(candidate, options))
            {
                continue;
            }

            includedCandidates.Add(candidate);

            if (persistedEffectiveIdsByKey.TryGetValue(candidate.RelativePathNoExt, out var persistedIds))
            {
                tagCache[candidate.RelativePathNoExt] = BuildTagComputationResultFromEffectiveIds(candidate, persistedIds);
                continue;
            }

            if (IsOptifinePath(candidate.RelativePathNoExt))
            {
                optifineCandidates++;
                if (IsNumericOnlyOptifineTile(candidate.Name, candidate.RelativePathNoExt))
                {
                    optifineNumericMlSkipped++;
                }
            }

            if (ShouldRunSemanticMlForCandidate(candidate, options, rules))
            {
                mlCandidates.Add(candidate);
            }
            else
            {
                noMlCandidates.Add(candidate);
            }
        }

        var optifineFolderMaterialHints = BuildNumericOptifineFolderMaterialHints(
            noMlCandidates,
            options,
            rules,
            cancellationToken);

        Parallel.ForEach(
            noMlCandidates,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = ResolveMaxParallelism(options)
            },
            candidate =>
            {
                IReadOnlyCollection<string>? inheritedMaterialTagIds = null;
                if (IsNumericOnlyOptifineTile(candidate.Name, candidate.RelativePathNoExt) &&
                    GetParentRelativePathNoExt(candidate.RelativePathNoExt) is { } parentKey &&
                    optifineFolderMaterialHints.TryGetValue(parentKey, out var parentMaterialIds))
                {
                    inheritedMaterialTagIds = parentMaterialIds;
                }

                var computed = ComputeTagsForCandidate(
                    candidate,
                    options,
                    rules,
                    deferSemanticMl: true,
                    inheritedMaterialTagIds);
                tagCache.TryAdd(candidate.RelativePathNoExt, computed);
            });

        // Phase 2: only ambiguous textures run semantic ML.
        var totalWork = totalCandidates + mlCandidates.Count;
        var optifineMlCandidates = mlCandidates.Count(static c =>
            IsOptifinePath(c.RelativePathNoExt));
        progress?.Report(new ConversionProgress(
            ConversionStage.ScanningTextures,
            totalCandidates,
            Math.Max(1, totalWork),
            null,
            $"Scan candidates: {totalCandidates} (OptiFine: {optifineCandidates}) | ML candidates: {mlCandidates.Count} (OptiFine: {optifineMlCandidates}) | OptiFine numeric ML skipped: {optifineNumericMlSkipped} | OptiFine folder hints: {optifineFolderMaterialHints.Count} | Cached tags: {persistedEffectiveIdsByKey.Count}"));
        var mlDone = 0;
        foreach (var candidate in mlCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tagCache.ContainsKey(candidate.RelativePathNoExt))
            {
                continue;
            }

            tagCache[candidate.RelativePathNoExt] = ComputeTagsForCandidate(candidate, options, rules, deferSemanticMl: false);
            mlDone++;
            progress?.Report(new ConversionProgress(
                ConversionStage.ScanningTextures,
                totalCandidates + mlDone,
                Math.Max(1, totalWork),
                Path.GetFileName(candidate.File)));
        }

        foreach (var candidate in includedCandidates)
        {
            if (!tagCache.TryGetValue(candidate.RelativePathNoExt, out var tags))
            {
                continue;
            }

            if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && tags.Sprite2DFoliageTarget)
            {
                continue;
            }

            results.Add(BuildWorkItem(candidate, tags, options));
        }

        var persistMap = tagCache.ToDictionary(
            static kv => kv.Key,
            static kv => kv.Value.EffectiveTagIds,
            StringComparer.OrdinalIgnoreCase);
        SharedEffectiveTagsCachePersistence.Save(
            cachePackPath ?? extractedPackRoot,
            signature,
            persistMap);

        return results;
    }
}

