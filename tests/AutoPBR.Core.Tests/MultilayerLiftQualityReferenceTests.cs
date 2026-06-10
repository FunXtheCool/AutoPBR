using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Multi-layer / scaled / villager-hat / cape pilots (26.1.2): reference_java bakes vs committed ok IR shards.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "LiftQuality")]
public sealed class MultilayerLiftQualityReferenceTests
{
    /// <summary>Ok shards with referenceCuboidsMatch.</summary>
    private static readonly string[] MultilayerJvmNames =
    [
        "net.minecraft.client.model.effects.SpinAttackEffectModel",
        "net.minecraft.client.model.monster.witch.WitchModel",
        "net.minecraft.client.model.monster.strider.AdultStriderModel",
        "net.minecraft.client.model.monster.strider.StriderModel",
        "net.minecraft.client.model.monster.nautilus.ZombieNautilusCoralModel",
        "net.minecraft.client.model.player.PlayerCapeModel",
    ];

    public static IEnumerable<object[]> MultilayerCases() => MultilayerJvmNames.Select(j => new object[] { j });

    [Theory]
    [MemberData(nameof(MultilayerCases))]
    public void Multilayer_reference_cuboids_match_committed_ok_shard(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        Assert.True(File.Exists(referencePath), $"missing reference bake: {jvm}");
        Assert.True(File.Exists(irPath), $"missing IR shard: {jvm}");
        Assert.True(
            GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var status) &&
            string.Equals(status, "ok", StringComparison.Ordinal),
            $"{jvm} shard must be ok");

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        Assert.Equal("reference_java", reference.RootElement.GetProperty("extractionStatus").GetString());

        using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message ?? jvm);
    }

    [Fact]
    public void Multilayer_quality_report_marks_all_reference_cuboids_match()
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var reportPath = Path.Combine(root, "docs", "generated", "geometry-lift-quality-26.1.2.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var byJvm = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in report.RootElement.GetProperty("entries").EnumerateArray())
        {
            var name = entry.GetProperty("officialJvmName").GetString();
            if (!string.IsNullOrEmpty(name))
            {
                byJvm[name] = entry;
            }
        }

        foreach (var jvm in MultilayerJvmNames)
        {
            Assert.True(byJvm.TryGetValue(jvm, out var row), $"quality report missing {jvm}");
            Assert.True(
                row.TryGetProperty("referenceCuboidsMatch", out var match) &&
                match.ValueKind == JsonValueKind.True,
                $"{jvm}: referenceCuboidsMatch={match}");
        }
    }
}
