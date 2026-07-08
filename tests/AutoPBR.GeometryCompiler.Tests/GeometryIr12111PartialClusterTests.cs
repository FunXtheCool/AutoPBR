using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// T3: clusters 1.21.11 partial index rows by extractionNotes for lift backlog triage.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class GeometryIr12111PartialClusterTests
{
    private static readonly JsonSerializerOptions ClusterJsonOptions = new() { WriteIndented = true };

    private static readonly string[] PromotionPilotJvmNames =
    [
        "net.minecraft.client.model.animal.fish.CodModel",
        "net.minecraft.client.model.animal.fish.SalmonModel",
        "net.minecraft.client.model.animal.goat.GoatModel",
        "net.minecraft.client.model.animal.panda.PandaModel",
        "net.minecraft.client.model.animal.dolphin.DolphinModel",
        "net.minecraft.client.model.monster.slime.MagmaCubeModel",
        "net.minecraft.client.model.monster.blaze.BlazeModel",
        "net.minecraft.client.model.monster.guardian.GuardianModel",
        "net.minecraft.client.model.animal.chicken.ColdChickenModel",
        "net.minecraft.client.model.animal.cow.WarmCowModel",
    ];

    [Fact]
    public void Write_partial_cluster_json_when_env_set()
    {
        var writePath = Environment.GetEnvironmentVariable("AUTOPBR_WRITE_GEOMETRY_12111_PARTIAL_CLUSTER");
        if (string.IsNullOrWhiteSpace(writePath))
        {
            return;
        }

        var root = Program.FindRepoRoot();
        if (!Path.IsPathRooted(writePath))
        {
            writePath = Path.Combine(root, writePath);
        }

        var indexPath = Path.Combine(root, "docs", "generated", "geometry-index-1.21.11.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var clusters = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in index.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!string.Equals(row.GetProperty("extractionStatus").GetString(), "partial", StringComparison.Ordinal))
            {
                continue;
            }

            var jvm = row.GetProperty("officialJvmName").GetString() ?? "";
            var note = row.TryGetProperty("extractionNotes", out var n)
                ? n.GetString() ?? "(none)"
                : "(none)";
            if (!clusters.TryGetValue(note, out var list))
            {
                list = [];
                clusters[note] = list;
            }

            list.Add(jvm);
        }

        var payload = new
        {
            schemaVersion = 1,
            versionLabel = "1.21.11",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            clusterCount = clusters.Count,
            clusters = clusters.OrderByDescending(kv => kv.Value.Count)
                .ToDictionary(kv => kv.Key, kv => kv.Value.Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal)
        };

        var dir = Path.GetDirectoryName(writePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(writePath, JsonSerializer.Serialize(payload, ClusterJsonOptions));
        Assert.True(File.Exists(writePath));
        Assert.True(clusters.Count > 0);
    }

    [Fact]
    public void Partial_rows_cluster_by_extraction_notes()
    {
        var root = Program.FindRepoRoot();
        var indexPath = Path.Combine(root, "docs", "generated", "geometry-index-1.21.11.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var clusters = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in index.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!string.Equals(row.GetProperty("extractionStatus").GetString(), "partial", StringComparison.Ordinal))
            {
                continue;
            }

            var jvm = row.GetProperty("officialJvmName").GetString() ?? "";
            var note = row.TryGetProperty("extractionNotes", out var n)
                ? n.GetString() ?? "(none)"
                : "(none)";
            if (!clusters.TryGetValue(note, out var list))
            {
                list = [];
                clusters[note] = list;
            }

            list.Add(jvm);
        }

        if (clusters.Count == 0)
        {
            return;
        }

        Assert.True(clusters.Count > 0);
    }

    [Theory]
    [MemberData(nameof(PromotionPilotCases))]
    public void Obfuscated_jar_lift_produces_ok_or_heuristic_cuboids(string jvm)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        var mapsPath = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");
        MojangMappingsParser? maps = File.Exists(mapsPath) ? MojangMappingsParser.Load(mapsPath) : null;
        var javap = GeometryJavapLocator.FindJavap();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, maps, jvm, "createBodyLayer", preferAsm: true,
                out var attempt),
            $"{jvm}: {string.Join("; ", attempt.Notes)}");
        Assert.True(CountCuboids(attempt.Roots) > 0, jvm);
    }

    public static IEnumerable<object[]> PromotionPilotCases() =>
        PromotionPilotJvmNames.Select(j => new object[] { j });

    [Theory]
    [InlineData("net.minecraft.client.model.animal.equine.AbstractEquineModel")]
    [InlineData("net.minecraft.client.model.monster.piglin.AbstractPiglinModel")]
    public void Obfuscated_abstract_mesh_hosts_lift_with_mapped_factory_pins(string jvm)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        var mapsPath = Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11",
            "client_mappings.txt");
        if (!File.Exists(mapsPath))
        {
            mapsPath = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");
        }

        var maps = MojangMappingsParser.Load(mapsPath);
        var javap = GeometryJavapLocator.FindJavap();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, maps, jvm, "createBodyLayer", preferAsm: true,
                out var attempt),
            $"{jvm}: {string.Join("; ", attempt.Notes)}");
        Assert.True(CountCuboids(attempt.Roots) > 0, jvm);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.monster.endermite.EndermiteModel")]
    [InlineData("net.minecraft.client.model.monster.silverfish.SilverfishModel")]
    public void Obfuscated_segment_name_helpers_lift_zero_cuboid_partial_backlog(string jvm)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        var mapsPath = Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11",
            "client_mappings.txt");
        if (!File.Exists(mapsPath))
        {
            mapsPath = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");
        }

        var maps = MojangMappingsParser.Load(mapsPath);
        var javap = GeometryJavapLocator.FindJavap();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, maps, jvm, "createBodyLayer", preferAsm: true,
                out var attempt),
            $"{jvm}: {string.Join("; ", attempt.Notes)}");
        Assert.True(CountCuboids(attempt.Roots) > 0, $"{jvm}: expected obfuscated segment helper lift to emit cuboids");
        Assert.DoesNotContain(0f, CollectCuboidWidths(attempt.Roots));
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var r in roots)
        {
            if (r is JsonObject ro)
            {
                n += CountPart(ro);
            }
        }

        return n;
    }

    private static IEnumerable<float> CollectCuboidWidths(JsonArray roots)
    {
        foreach (var root in roots.OfType<JsonObject>())
        {
            foreach (var width in CollectCuboidWidths(root))
            {
                yield return width;
            }
        }
    }

    private static IEnumerable<float> CollectCuboidWidths(JsonObject part)
    {
        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboid in cuboids.OfType<JsonObject>())
            {
                if (cuboid["from"] is JsonArray from && cuboid["to"] is JsonArray to &&
                    from.Count >= 1 && to.Count >= 1)
                {
                    yield return to[0]!.GetValue<float>() - from[0]!.GetValue<float>();
                }
            }
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var child in kids.OfType<JsonObject>())
            {
                foreach (var width in CollectCuboidWidths(child))
                {
                    yield return width;
                }
            }
        }
    }

    private static int CountPart(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray c ? c.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    n += CountPart(co);
                }
            }
        }

        return n;
    }
}
