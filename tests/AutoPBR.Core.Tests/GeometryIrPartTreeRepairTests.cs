using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Phase 5A — preview part-tree repair must not break flat Java quadruped bakes (creeper canary).
/// </summary>
public sealed class GeometryIrPartTreeRepairTests
{
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";
    private const string ChickenJvm = "net.minecraft.client.model.animal.chicken.ChickenModel";

    [Fact]
    public void Creeper_flat_bake_repair_preserves_world_part_origins()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CreeperJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(CreeperJvm, raw);

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Creeper_repair_does_not_stack_body_y_onto_leg_origins()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CreeperJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(CreeperJvm, shard.RootElement);
        Assert.True(
            GeometryIrMeshWalk.TryCollectPartWorldTranslations(geometryRoot, Matrix4x4.Identity, out var byPart, out var walkFail),
            walkFail);
        Assert.True(byPart.TryGetValue("right_hind_leg", out var legOrigin));
        Assert.InRange(legOrigin.Y, 17.5f, 18.5f);
        Assert.True(byPart.TryGetValue("body", out var bodyOrigin));
        Assert.InRange(bodyOrigin.Y, 5.5f, 6.5f);
        Assert.False(byPart.Values.Any(v => Math.Abs(v.Y - 24f) < 0.1f),
            "leg origins must not pick up stacked body+leg Y from harmful reparent");
    }

    [Fact]
    public void Chicken_ok_shard_still_reparents_beak_under_head()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{ChickenJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(ChickenJvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "beak", "head"), "beak should nest under head after repair");
    }

    [Fact]
    public void Baby_donkey_repair_nests_flat_leg_siblings_under_body()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyDonkeyModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "left_front_leg", "body"));
        Assert.True(PartNestedUnder(repaired, "right_hind_leg", "body"));
        Assert.True(PartNestedUnder(repaired, "tail_r1", "tail"));
        Assert.True(PartNestedUnder(repaired, "head", "head_parts"));
        Assert.True(PartNestedUnder(repaired, "head_r1", "head"));
        Assert.True(PartNestedUnder(repaired, "left_ear", "head"));
        Assert.True(PartNestedUnder(repaired, "neck_r1", "head_parts"));
    }

    [Fact]
    public void Baby_horse_repair_nests_ears_under_head()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyHorseModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "left_ear", "head"));
        Assert.True(PartNestedUnder(repaired, "right_ear", "head"));
    }

    [Fact]
    public void Baby_horse_flat_bake_does_not_reparent_root_sibling_legs()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyHorseModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.False(PartNestedUnder(repaired, "left_front_leg", "body"));
    }

    private static bool PartNestedUnder(JsonElement geometryRoot, string childId, string parentId)
    {
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children))
            {
                continue;
            }

            if (TryFindPart(children, parentId, out var parent) &&
                parent.TryGetProperty("children", out var parentKids) &&
                parentKids.EnumerateArray().Any(ch =>
                    ch.TryGetProperty("id", out var id) &&
                    string.Equals(id.GetString(), childId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPart(JsonElement parts, string id, out JsonElement found)
    {
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), id, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part.TryGetProperty("children", out var kids) && TryFindPart(kids, id, out found))
            {
                return true;
            }
        }

        found = default;
        return false;
    }

}
