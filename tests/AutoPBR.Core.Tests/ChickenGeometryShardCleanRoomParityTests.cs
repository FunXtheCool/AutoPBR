using System.Text.Json;

namespace AutoPBR.Core.Tests;

/// <summary>
/// T1: locks preview <c>Chicken</c> builder mesh literals to promoted <c>ChickenModel</c> IR
/// (same cuboids / poses as <c>ChickenModel.createBodyLayer</c>). See docs/test-guidance-geometry-animation-ir.md.
/// </summary>
public sealed class ChickenGeometryShardCleanRoomParityTests
{
    private static string ContentPath(params string[] segments) =>
        Path.Combine([GeometryIrTestTierSupport.FindRepoRoot(), .. segments]);

    [Theory]
    [InlineData("26.1.2")]
    [InlineData("1.21.11")]
    public void Geometry_index_lists_animal_chicken_shard(string versionLabel)
    {
        var indexPath = ContentPath("docs", "generated", $"geometry-index-{versionLabel}.json");
        Assert.True(File.Exists(indexPath), $"Missing test content: {indexPath}");

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var wantName = "net.minecraft.client.model.animal.chicken.ChickenModel";
        var wantRel = $"geometry/{versionLabel}/{wantName}.json";
        var found = false;
        foreach (var e in index.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (e.TryGetProperty("officialJvmName", out var jn) &&
                string.Equals(jn.GetString(), wantName, StringComparison.Ordinal) &&
                e.TryGetProperty("shardRelPath", out var sp))
            {
                Assert.Equal(wantRel, sp.GetString());
                found = true;
                break;
            }
        }

        Assert.True(found, $"geometry-index-{versionLabel} should list {wantName}.");
    }

    [Fact]
    public void Chicken_geometry_shard_26_1_2_cuboids_and_part_poses_match_clean_room_builder()
    {
        const string versionLabel = "26.1.2";
        var path = ContentPath("docs", "generated", "geometry", versionLabel,
            "net.minecraft.client.model.animal.chicken.ChickenModel.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var parts = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var ch in doc.RootElement.GetProperty("roots")[0].GetProperty("children").EnumerateArray())
        {
            parts[ch.GetProperty("id").GetString()!] = ch;
        }

        // 26.1.2 javap: head nests beak + red_thing; left_leg is a sibling of right_leg.
        Assert.Equal(6, parts.Count);

        JsonElement beak = default;
        JsonElement redThing = default;
        foreach (var ch in parts["head"].GetProperty("children").EnumerateArray())
        {
            var id = ch.GetProperty("id").GetString()!;
            if (string.Equals(id, "beak", StringComparison.Ordinal))
            {
                beak = ch;
            }
            else if (string.Equals(id, "red_thing", StringComparison.Ordinal))
            {
                redThing = ch;
            }
        }

        AssertCuboidExtents(parts["head"], 4, 6, 3);
        AssertTranslation(parts["head"], 0, 15, -4);

        AssertCuboidExtents(beak, 4, 2, 2);
        AssertTranslation(beak, 0, 0, 0);

        AssertCuboidExtents(redThing, 2, 2, 2);
        AssertTranslation(redThing, 0, 0, 0);

        AssertCuboidExtents(parts["body"], 6, 8, 6);
        AssertTranslation(parts["body"], 0, 16, 0);
        AssertRadApprox(parts["body"].GetProperty("pose").GetProperty("rotationEulerRad")[0].GetDouble(),
            Math.PI / 2, 1e-5);

        AssertCuboidExtents(parts["right_leg"], 3, 5, 3);
        AssertTranslation(parts["right_leg"], -2, 19, 1);

        AssertCuboidExtents(parts["left_leg"], 3, 5, 3);
        AssertTranslation(parts["left_leg"], 1, 19, 1);
        AssertUvOrigin(parts["left_leg"], 26, 0);

        AssertCuboidExtents(parts["right_wing"], 1, 4, 6);
        AssertTranslation(parts["right_wing"], -4, 13, 0);

        AssertCuboidExtents(parts["left_wing"], 1, 4, 6);
        AssertTranslation(parts["left_wing"], 4, 13, 0);

        AssertUvOrigin(parts["head"], 0, 0);
        AssertUvOrigin(beak, 14, 0);
        AssertUvOrigin(redThing, 14, 4);
        AssertUvOrigin(parts["body"], 0, 9);
        AssertUvOrigin(parts["right_leg"], 26, 0);
        AssertUvOrigin(parts["right_wing"], 24, 13);
        AssertUvOrigin(parts["left_wing"], 24, 13);
    }

    private static void AssertCuboidExtents(JsonElement part, float ex, float ey, float ez)
    {
        var c = part.GetProperty("cuboids")[0];
        var from = c.GetProperty("from");
        var to = c.GetProperty("to");
        Assert.Equal(ex, (float)(to[0].GetDouble() - from[0].GetDouble()), 5);
        Assert.Equal(ey, (float)(to[1].GetDouble() - from[1].GetDouble()), 5);
        Assert.Equal(ez, (float)(to[2].GetDouble() - from[2].GetDouble()), 5);
    }

    private static void AssertTranslation(JsonElement part, double x, double y, double z)
    {
        var t = part.GetProperty("pose").GetProperty("translation");
        Assert.Equal(x, t[0].GetDouble(), 8);
        Assert.Equal(y, t[1].GetDouble(), 8);
        Assert.Equal(z, t[2].GetDouble(), 8);
    }

    private static void AssertUvOrigin(JsonElement part, int u, int v)
    {
        var uv = part.GetProperty("cuboids")[0].GetProperty("uvOrigin");
        Assert.Equal(u, uv[0].GetInt32());
        Assert.Equal(v, uv[1].GetInt32());
    }

    private static void AssertRadApprox(double actual, double expected, double tol) =>
        Assert.InRange(actual, expected - tol, expected + tol);
}
