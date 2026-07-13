using Avalonia;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.Models;

/// <summary>Flattened Explore list row: archive node plus depth for tree-style indent under the focused folder.</summary>
public sealed partial class ExploreListRow(ArchiveNode node, int depth) : ObservableObject
{
    public const double IndentPerLevel = 16;

    public ArchiveNode Node { get; } = node;

    public int Depth { get; } = depth;

    public Thickness IndentMargin { get; } = new(depth * IndentPerLevel, 0, 0, 0);
}
