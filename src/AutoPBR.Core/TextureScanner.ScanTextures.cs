using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    public static IReadOnlyList<TextureWorkItem> ScanTextures(
        string extractedPackRoot,
        AutoPBROptions options,
        IProgress<ConversionProgress>? progress = null,
        string? cachePackPath = null,
        bool applyFoliageIgnoreFilter = true,
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

        var packBaseTileSize = EstimatePackBaseTileSize(includedCandidates);

        foreach (var candidate in includedCandidates)
        {
            if (!tagCache.TryGetValue(candidate.RelativePathNoExt, out var tags))
            {
                continue;
            }

            if (applyFoliageIgnoreFilter &&
                FoliageModeResolver.IsIgnoreAll(options.FoliageMode) &&
                tags.Sprite2DFoliageTarget)
            {
                continue;
            }

            results.Add(BuildWorkItem(candidate, tags, options, packBaseTileSize));
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
