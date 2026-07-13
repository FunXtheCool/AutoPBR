using AutoPBR.App.Models;

namespace AutoPBR.App.Tests;

public sealed class ExploreDisplayListBuilderTests
{
    [Fact]
    public void Build_IncludesExpandedDescendants_WithDepth()
    {
        var entity = new ArchiveNode("entity", "textures/entity", true, null, null);
        var cow = new ArchiveNode("cow", "textures/entity/cow", true, entity, null);
        var cowPng = new ArchiveNode("cow.png", "textures/entity/cow/cow.png", false, cow, null);
        var pig = new ArchiveNode("pig", "textures/entity/pig", true, entity, null);
        entity.Children.Add(cow);
        entity.Children.Add(pig);
        cow.Children.Add(cowPng);
        cow.IsExpanded = true;

        // Explore list roots are the focused folder's children (not the focus node itself).
        var rows = ExploreDisplayListBuilder.Build(entity.Children);

        Assert.Equal(3, rows.Count);
        Assert.Equal(("cow", 0), (rows[0].Node.Name, rows[0].Depth));
        Assert.Equal(("cow.png", 1), (rows[1].Node.Name, rows[1].Depth));
        Assert.Equal(("pig", 0), (rows[2].Node.Name, rows[2].Depth));
        Assert.Equal(0, rows[0].IndentMargin.Left);
        Assert.Equal(16, rows[1].IndentMargin.Left);
    }

    [Fact]
    public void Build_SkipsCollapsedFoldersAndHiddenNodes()
    {
        var root = new ArchiveNode("block", "textures/block", true, null, null);
        var visible = new ArchiveNode("stone.png", "textures/block/stone.png", false, root, null);
        var hidden = new ArchiveNode("dirt.png", "textures/block/dirt.png", false, root, null);
        var nested = new ArchiveNode("subdir", "textures/block/subdir", true, root, null);
        var nestedFile = new ArchiveNode("x.png", "textures/block/subdir/x.png", false, nested, null);
        root.Children.Add(visible);
        root.Children.Add(hidden);
        root.Children.Add(nested);
        nested.Children.Add(nestedFile);
        hidden.IsVisibleByFilter = false;
        nested.IsExpanded = false;

        var rows = ExploreDisplayListBuilder.Build(root.Children);

        Assert.Equal(2, rows.Count);
        Assert.Equal("stone.png", rows[0].Node.Name);
        Assert.Equal("subdir", rows[1].Node.Name);
        Assert.All(rows, r => Assert.Equal(0, r.Depth));
    }
}
