using AutoPBR.Core.Models;

namespace AutoPBR.App.Models;

/// <summary>Provides override storage, tag overrides, and lazy child loading for archive tree nodes.</summary>
public interface IArchiveNodeHost
{
    bool? GetOverride(string fullPath);
    void SetOverride(string fullPath, bool? value);
    void EnsureChildrenLoaded(ArchiveNode node);

    /// <summary>Effective tags for a file (auto from keywords plus manual add/remove). Empty for folders or if path is not a texture.</summary>
    IReadOnlyList<(string Id, string DisplayName)> GetEffectiveTags(string archivePath);

    /// <summary>Remove a tag from this path (user chose "don't apply here").</summary>
    void SetTagRemoved(string archivePath, string tagId);

    /// <summary>Add a tag to this path (user chose "apply here").</summary>
    void SetTagAdded(string archivePath, string tagId);

    /// <summary>All tag rules (for legend and context menu).</summary>
    IReadOnlyList<TagRule> GetTagRules();

    /// <summary>Localized context menu header for a tag: "Don't apply 'X' here" or "Apply 'X' here".</summary>
    string GetTagMenuHeader(string displayName, bool isApplied);
}
