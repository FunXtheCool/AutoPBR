using System.Collections.Concurrent;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    private void PersistTagOverrides()
    {
        if (string.IsNullOrEmpty(_scannedArchivePath))
        {
            return;
        }

        TagOverridesPersistence.Save(_scannedArchivePath, GetManualTagOverrides());
    }

    public IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)> GetManualTagOverrides()
    {
        var keys = new HashSet<string>(_tagAdded.Keys.Union(_tagRemoved.Keys), StringComparer.OrdinalIgnoreCase);
        var dict = new Dictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var added = _tagAdded.TryGetValue(key, out var a) ? (IReadOnlyList<string>)a.ToList() : [];
            var removed = _tagRemoved.TryGetValue(key, out var r) ? (IReadOnlyList<string>)r.ToList() : [];
            dict[key] = (added, removed);
        }

        return dict;
    }

    private string ComputeEffectiveTagCacheSignature()
    {
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var manualOverrides = new Dictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in _tagAdded.Keys.Union(_tagRemoved.Keys))
        {
            IReadOnlyList<string> added = [];
            if (_tagAdded.TryGetValue(key, out var addSet))
            {
                lock (addSet)
                {
                    added = addSet.ToList();
                }
            }

            IReadOnlyList<string> removed = [];
            if (_tagRemoved.TryGetValue(key, out var remSet))
            {
                lock (remSet)
                {
                    removed = remSet.ToList();
                }
            }

            manualOverrides[key] = (added, removed);
        }

        return SharedEffectiveTagsCacheSignature.Compute(rules, sem, manualOverrides);
    }

    private void TryLoadEffectiveTagsCache()
    {
        if (string.IsNullOrWhiteSpace(_scannedArchivePath))
        {
            return;
        }

        var snapshot = SharedEffectiveTagsCachePersistence.Load(_scannedArchivePath);
        if (snapshot is null)
        {
            _debugSink?.Invoke("Explore cache: no persisted effective-tag cache found.");
            return;
        }

        var signature = ComputeEffectiveTagCacheSignature();
        if (!string.Equals(snapshot.Signature, signature, StringComparison.Ordinal))
        {
            _debugSink?.Invoke("Explore cache: signature mismatch, persisted cache ignored.");
            return;
        }

        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        foreach (var kv in snapshot.EffectiveTagIdsByStorageKey)
        {
            var tuples = new List<(string Id, string DisplayName, TagRuleKind Kind)>();
            foreach (var id in kv.Value)
            {
                var rule = rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
                if (rule is not null)
                {
                    tuples.Add((rule.Id, rule.DisplayName, rule.Kind));
                }
            }

            var archivePath = StorageKeyToArchivePath(kv.Key);
            if (archivePath is not null)
            {
                _effectiveTagCache[archivePath] = tuples;
            }

            _effectiveTagIdsByStorageKey[kv.Key] = kv.Value;
        }

        _debugSink?.Invoke($"Explore cache: loaded {_effectiveTagIdsByStorageKey.Count} effective-tag entries.");
    }

    private void PersistEffectiveTagCache()
    {
        if (string.IsNullOrWhiteSpace(_scannedArchivePath))
        {
            return;
        }

        SharedEffectiveTagsCachePersistence.Save(
            _scannedArchivePath,
            ComputeEffectiveTagCacheSignature(),
            _effectiveTagIdsByStorageKey);
    }

    private string? StorageKeyToArchivePath(string storageKey)
    {
        if (Data is null)
        {
            return null;
        }

        if (Data.IsBatch)
        {
            var split = storageKey.IndexOf('|');
            if (split <= 0)
            {
                return null;
            }

            var packRoot = storageKey[..split];
            var key = storageKey[(split + 1)..];
            var archive = TextureKeyToArchivePath(key);
            return archive is null ? null : packRoot + "/" + archive;
        }

        return TextureKeyToArchivePath(storageKey);
    }

    private static string? TextureKeyToArchivePath(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return "assets/" + normalized + ".png";
    }
}
