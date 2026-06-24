using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Preview;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Javap-strength lift locks for monster and happy ghast body/tentacle factories.
/// </summary>
public sealed class GhastFamilyLiftTests
{
    private const string MonsterJvm = "net.minecraft.client.model.monster.ghast.GhastModel";
    private const string HappyJvm = "net.minecraft.client.model.animal.ghast.HappyGhastModel";

    private static readonly int[] MonsterTentacleHeights = [8, 13, 9, 11, 11, 10, 12, 9, 12];
    private static readonly int[] HappyTentacleHeights = [5, 7, 4, 5, 5, 7, 8, 8, 5];

    public static IEnumerable<object[]> GhastLiftCases() =>
    [
        [MonsterJvm, MonsterTentacleHeights],
        [HappyJvm, HappyTentacleHeights],
    ];

    [Theory]
    [MemberData(nameof(GhastLiftCases))]
    public void Ghast_family_jar_lift_matches_reference_cuboids_and_tentacle_heights(string jvm, int[] tentacleHeights)
    {
        var repo = Program.FindRepoRoot();
        var jar = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "client.jar");
        var referencePath = Path.Combine(
            repo,
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            $"{jvm}.json");
        if (!File.Exists(jar) || !File.Exists(referencePath))
        {
            // T2 probe: optional when parity jar/reference tree is absent locally.
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                preferAsm: true, out var attempt),
            string.Join("; ", attempt.Notes));

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var shardRoot = BuildShardRoot(jvm, attempt.Roots);
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement,
            shardRoot,
            tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);

        Assert.Equal(1, CountPart(shardRoot, "body"));
        for (var i = 0; i < tentacleHeights.Length; i++)
        {
            var tentacleId = $"tentacle{i}";
            Assert.Equal(1, CountPart(shardRoot, tentacleId));
            var height = ReadCuboidHeight(shardRoot, tentacleId);
            Assert.Equal(tentacleHeights[i], height);
            // Lifted IR stores javap +Y boxes before emit reorient; height must be positive span.
            Assert.True(height >= 4, $"{tentacleId} lift height should match javap addBox (+Y), got {height}");
        }
    }

    [Theory]
    [InlineData("net.minecraft.client.model.monster.ghast.GhastModel")]
    [InlineData("net.minecraft.client.model.animal.ghast.HappyGhastModel")]
    public void Ghast_family_committed_shard_tentacle_heights_match_javap_reference(string jvm)
    {
        var repo = Program.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        Assert.True(File.Exists(shardPath), $"missing committed shard: {shardPath}");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var status = shard.RootElement.GetProperty("extractionStatus").GetString();
        Assert.Equal("ok", status);

        var expected = string.Equals(jvm, "net.minecraft.client.model.monster.ghast.GhastModel", StringComparison.Ordinal)
            ? new[] { 8, 13, 9, 11, 11, 10, 12, 9, 12 }
            : new[] { 5, 7, 4, 5, 5, 7, 8, 8, 5 };
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ReadCuboidHeight(shard.RootElement, $"tentacle{i}"));
        }
    }

    [Theory]
    [InlineData("net.minecraft.client.model.monster.ghast.GhastModel")]
    [InlineData("net.minecraft.client.model.monster.Ghast.GhastModel")]
    [InlineData("net.minecraft.client.model.GhastModel")]
    [InlineData("net.minecraft.client.model.animal.ghast.HappyGhastModel")]
    public void Ghast_family_emit_policy_recognizes_jvm_name_variants(string jvm)
    {
        Assert.True(GeometryIrEmitPolicy.IsGhastFamilyJvm(jvm));
        Assert.False(GeometryIrEmitPolicy.IsGhastFamilyJvm("net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel"));
    }

    private static JsonElement BuildShardRoot(string jvm, JsonArray roots)
    {
        var node = new JsonObject
        {
            ["extractionStatus"] = "ok",
            ["officialJvmName"] = jvm,
            ["roots"] = roots.DeepClone(),
        };
        return JsonDocument.Parse(node.ToJsonString()).RootElement;
    }

    private static int CountPart(JsonElement shardRoot, string partId)
    {
        var count = 0;
        if (!shardRoot.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var root in roots.EnumerateArray())
        {
            count += WalkPartCount(root, partId);
        }

        return count;
    }

    private static int WalkPartCount(JsonElement part, string partId)
    {
        var count = 0;
        if (part.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
        {
            count++;
        }

        if (part.TryGetProperty("children", out var kids))
        {
            foreach (var kid in kids.EnumerateArray())
            {
                count += WalkPartCount(kid, partId);
            }
        }

        return count;
    }

    private static int ReadCuboidHeight(JsonElement shardRoot, string partId)
    {
        if (!shardRoot.TryGetProperty("roots", out var roots))
        {
            return -1;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryReadCuboidHeight(root, partId, out var height))
            {
                return height;
            }
        }

        return -1;
    }

    private static bool TryReadCuboidHeight(JsonElement part, string partId, out int height)
    {
        height = -1;
        if (part.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal) &&
            part.TryGetProperty("cuboids", out var cuboids) &&
            cuboids.ValueKind == JsonValueKind.Array &&
            cuboids.GetArrayLength() > 0)
        {
            var cuboid = cuboids[0];
            var y0 = cuboid.GetProperty("from")[1].GetInt32();
            var y1 = cuboid.GetProperty("to")[1].GetInt32();
            height = Math.Abs(y1 - y0);
            return true;
        }

        if (!part.TryGetProperty("children", out var kids))
        {
            return false;
        }

        foreach (var kid in kids.EnumerateArray())
        {
            if (TryReadCuboidHeight(kid, partId, out height))
            {
                return true;
            }
        }

        return false;
    }
}
