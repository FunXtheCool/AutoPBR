using AutoPBR.Core.Models;

namespace AutoPBR.App.Models;

/// <summary>Provides override storage, tag overrides, and lazy child loading for archive tree nodes.</summary>
public interface IArchiveNodeHost
{
    bool? GetOverride(string fullPath);
    void SetOverride(string fullPath, bool? value);
    void EnsureChildrenLoaded(ArchiveNode node);

    /// <summary>Effective tags for a file (keyword + optional ML matches, plus manual add/remove). Empty for folders or if path is not a texture.</summary>
    IReadOnlyList<(string Id, string DisplayName, TagRuleKind Kind)> GetEffectiveTags(string archivePath);

    /// <summary>
    /// Apply or remove a material/flag tag for this texture. Handles both automatic tags (hide via removed)
    /// and manually added tags (drop from added; add to removed only if the tag would still appear from rules/ML).
    /// </summary>
    void ApplyManualTagToggle(string archivePath, string tagId, bool wantApplied);

    /// <summary>All tag rules (for legend and context menu).</summary>
    IReadOnlyList<TagRule> GetTagRules();

    /// <summary>
    /// Notify that Explore list structure may have changed (expand/collapse or children loaded)
    /// so the virtualized flat list can rebuild.
    /// </summary>
    void NotifyExploreStructureChanged();
}
