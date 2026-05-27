using System.Collections.Concurrent;
using System.IO.Compression;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Media.Imaging;

namespace AutoPBR.App.Services;

/// <summary>Owns scanned archive data, path overrides, folder visibility cache, and the explore tree root. Implements <see cref="IArchiveNodeHost"/> for lazy loading and override storage.</summary>
internal sealed partial class ExploreTreeController : IArchiveNodeHost, IDisposable
{
    private static readonly HashSet<string> TextureTypeFolderNames = new(StringComparer.OrdinalIgnoreCase)
        { "block", "blocks", "item", "items", "entity", "particle" };

    private static readonly HashSet<string> IgnoredOptifineFolders = new(StringComparer.OrdinalIgnoreCase)
        { "anim", "colormap", "sky" };

    private string? _scannedArchivePath;
    private readonly ConcurrentDictionary<string, bool?> _pathOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagAdded = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagRemoved = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _folderVisibilityCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bitmap?> _batchPackIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _batchPackIconLoading = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _batchPackIconSync = new();
    private string _exploreFilter = "";
    private string? _exploreTagFilterId;
    private Func<IReadOnlyList<TagRule>>? _tagRulesProvider;
    private Func<MaterialTagSemanticOptions?>? _materialTagSemanticOptionsProvider;
    private IBackgroundTaskSink? _backgroundTaskSink;
    private Action<string>? _debugSink;

    /// <summary>Maps texture storage key → effective tag ids for &quot;Show tag&quot; filtering (avoids re-running ML/keywords per node on every filter pass).</summary>
    private Dictionary<string, HashSet<string>>? _exploreTagFilterCache;

    /// <summary>Pre-computed effective tags per archive path. Populated by background refresh; read by <see cref="IArchiveNodeHost.GetEffectiveTags"/>.</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<(string Id, string DisplayName, TagRuleKind Kind)>> _effectiveTagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _effectiveTagIdsByStorageKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _optifineFolderMaterialHintIdsByRuleKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _effectiveTagComputeInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _finalSemanticTagPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bumped when tag semantics are refreshed so in-flight per-path background computes cannot overwrite newer cache entries.</summary>
    private int _effectiveTagEpoch;

    private CancellationTokenSource? _tagRefreshCts;
    private CancellationTokenSource? _refreshDisplayTagsDebounceCts;

    /// <summary>Background tag work: per-path deferred MiniLM/dictionary and full-tree refresh. Conversion waits for this to reach 0.</summary>
    private int _tagAsyncWorkPending;

    /// <summary>Last full-tree tag refresh; conversion may await this so ONNX work does not overlap convert.</summary>
    private Task? _tagRefreshAllTask;

    /// <summary>Limits parallel per-texture ML/dictionary work so completion callbacks do not flood the UI thread.</summary>
    private readonly SemaphoreSlim _tagComputeConcurrency = new(4, 4);

    public ArchiveNode? Root { get; private set; }
    public ScannedArchiveData? Data { get; private set; }

    /// <summary>Optional UI progress for background tag work (dictionary + ML per texture).</summary>
    public void SetBackgroundTaskSink(IBackgroundTaskSink? sink) => _backgroundTaskSink = sink;
    public void SetDebugSink(Action<string>? sink) => _debugSink = sink;

    public void Dispose()
    {
        PersistEffectiveTagCache();
        _tagRefreshCts?.Cancel();
        _refreshDisplayTagsDebounceCts?.Cancel();
        _tagRefreshCts?.Dispose();
        _refreshDisplayTagsDebounceCts?.Dispose();
        _tagComputeConcurrency.Dispose();
    }
}
