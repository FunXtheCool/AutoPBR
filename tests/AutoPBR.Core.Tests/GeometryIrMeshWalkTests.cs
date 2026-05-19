using System.Text.Json;
using AutoPBR.Tests.Shared;


namespace AutoPBR.Core.Tests;

public sealed class GeometryIrMeshWalkTests
{
    [Fact]
    public void CollectCuboidOwnerPartIds_honors_IncludePartIds_filter()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var path = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.ChickenModel.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(path, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var all = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(root, GeometryIrMeshEmitOptions.ForParity());
        var legsOnly = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            root,
            GeometryIrMeshEmitOptions.ForParity() with
            {
                IncludePartIds = new HashSet<string>(StringComparer.Ordinal) { "body", "left_leg", "right_leg" }
            });
        Assert.True(legsOnly.Count < all.Count);
        Assert.All(legsOnly, id => Assert.True(id is "body" or "left_leg" or "right_leg"));
    }
}
