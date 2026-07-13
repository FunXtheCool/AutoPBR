using System.Collections.ObjectModel;

namespace AutoPBR.App.Models;

/// <summary>Builds the flattened Explore list (filter + expand) for the virtualized ListBox.</summary>
public static class ExploreDisplayListBuilder
{
    public static void BuildInto(ObservableCollection<ExploreListRow> target, IEnumerable<ArchiveNode> roots)
    {
        target.Clear();
        foreach (var n in roots)
        {
            Walk(target, n, 0);
        }
    }

    public static List<ExploreListRow> Build(IEnumerable<ArchiveNode> roots)
    {
        var list = new List<ExploreListRow>();
        foreach (var n in roots)
        {
            Walk(list, n, 0);
        }

        return list;
    }

    private static void Walk(ICollection<ExploreListRow> target, ArchiveNode n, int depth)
    {
        if (!n.IsVisibleByFilter)
        {
            return;
        }

        target.Add(new ExploreListRow(n, depth));
        if (n is { IsFolder: true, IsExpanded: true })
        {
            foreach (var ch in n.Children)
            {
                Walk(target, ch, depth + 1);
            }
        }
    }
}
