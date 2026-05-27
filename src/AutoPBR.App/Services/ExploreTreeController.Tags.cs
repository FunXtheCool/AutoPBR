using System.Collections.Concurrent;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    private string? ResolveTagStorageKey(string archivePath)
    {
        if (TryStripBatchPackRoot(archivePath, out var packRoot, out var inner))
        {
            var key = ArchivePathToTextureKey(inner);
            return key is null ? null : packRoot + "|" + key;
        }

        return ArchivePathToTextureKey(archivePath);
    }

    private static string RuleRelativeKeyFromStorageKey(string storageKey)
    {
        var i = storageKey.IndexOf('|');
        return i < 0 ? storageKey : storageKey[(i + 1)..];
    }

    IReadOnlyList<(string Id, string DisplayName, TagRuleKind Kind)> IArchiveNodeHost.GetEffectiveTags(string archivePath)
    {
        if (_effectiveTagCache.TryGetValue(archivePath, out var cached))
        {
            var cachedSem = _materialTagSemanticOptionsProvider?.Invoke();
            if (cachedSem is { Enabled: true, Matcher: not null } && !_finalSemanticTagPaths.ContainsKey(archivePath))
            {
                _ = QueueBackgroundEffectiveTagComputeAsync(archivePath);
            }

            return cached;
        }

        // When MiniLM is enabled, do not run ONNX/dictionary on the UI thread: keyword tags first, then async enrichment.
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var deferMl = sem is { Enabled: true, Matcher: not null };
        var immediate = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: false, deferSemanticMl: deferMl);
        _effectiveTagCache[archivePath] = immediate;
        _finalSemanticTagPaths.TryRemove(archivePath, out _);
        if (deferMl)
        {
            _ = QueueBackgroundEffectiveTagComputeAsync(archivePath);
        }

        return immediate;
    }
}
