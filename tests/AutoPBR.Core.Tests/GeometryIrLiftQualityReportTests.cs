using System.Text.Json;
using AutoPBR.Tests.TestSupport;


namespace AutoPBR.Core.Tests;

/// <summary>T3 diagnostic — index-wide lift quality (opt-in). See docs/test-guidance-geometry-animation-ir.md.</summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "LiftQuality")]
public sealed class GeometryIrLiftQualityReportTests
{
    [Fact]
    public void Report_chicken_ok_shard_has_nested_beak_not_flat_at_root()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.ChickenModel.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(
            "net.minecraft.client.model.animal.chicken.ChickenModel",
            "ok",
            shard.RootElement,
            repo);
        Assert.Equal(0, entry.SuspectedFlatNestedPartCount);
        Assert.True(entry.CuboidCount >= 6);
        Assert.True(entry.ReferenceHierarchyMatch);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.monster.creeper.CreeperModel")]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("net.minecraft.client.model.animal.sheep.SheepModel")]
    public void Vanilla_flat_quadruped_pilot_matches_reference_hierarchy_when_flat_at_root(string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvmName}.json");
        if (!File.Exists(refPath) ||
            !GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvmName, "ok", shard.RootElement, repo);
        Assert.True(entry.SuspectedFlatNestedPartCount > 0);
        Assert.True(entry.ReferenceHierarchyMatch, entry.ReferenceHierarchyMessage);
    }

    [Fact]
    public void Creeper_ok_shard_passes_assembly_gate_when_reference_gates_align()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.monster.creeper.CreeperModel.json");
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            "net.minecraft.client.model.monster.creeper.CreeperModel.json");
        if (!File.Exists(refPath) ||
            !GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(
            "net.minecraft.client.model.monster.creeper.CreeperModel",
            "ok",
            shard.RootElement,
            repo);
        Assert.True(entry.ReferenceHierarchyMatch);
        Assert.False(entry.ExtractionBindingGap);
        Assert.True(entry.AssemblyGatePass);
    }

    [Fact]
    public void Index_false_green_backlog_count_meets_pilot_threshold()
    {
        if (!GeometryIrTestTierSupport.RunLiftQualityIndexDiagnostics())
        {
            return;
        }

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var indexPath = Path.Combine(repo, "docs", "generated", "geometry-index-26.1.2.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var doc = GeometryIrLiftQualityReport.BuildForIndex(repo, "26.1.2", index.RootElement);
        var falseGreenCount = doc.Entries.Count(e =>
            e.SuspectedFlatNestedPartCount > 0 && e.AllReferenceLocalsMatch);
        Assert.True(falseGreenCount >= 50, $"expected >= 50 false-green ok entries, got {falseGreenCount}");
    }

    [Fact]
    public void Write_quality_report_when_env_set()
    {
        var writePath = Environment.GetEnvironmentVariable("AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY");
        if (string.IsNullOrWhiteSpace(writePath))
        {
            return;
        }

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!Path.IsPathRooted(writePath))
        {
            writePath = Path.Combine(repo, writePath);
        }

        var indexPath = Path.Combine(repo, "docs", "generated", "geometry-index-26.1.2.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var doc = GeometryIrLiftQualityReport.BuildForIndex(repo, "26.1.2", index.RootElement);
        GeometryIrLiftQualityReport.WriteJson(doc, writePath);
        Assert.True(File.Exists(writePath));
        Assert.True(doc.OkEntryCount > 0);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.monster.creeper.CreeperModel")]
    [InlineData("net.minecraft.client.model.QuadrupedModel")]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel")]
    public void Vanilla_flat_quadruped_pilot_passes_world_pose_gate_when_flat_at_root(string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvmName}.json");
        if (!File.Exists(refPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvmName, "ok", shard.RootElement, repo);
        Assert.True(entry.AllReferenceLocalsMatch, $"expected local reference gates to pass for {jvmName}");
        Assert.True(entry.ReferenceHierarchyMatch, entry.ReferenceHierarchyMessage);
        Assert.Equal(true, entry.ReferenceWorldPoseMatch);
    }

    [Fact]
    public void Assembly_parity_pilot_list_has_expected_scope()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var pilots = GeometryAssemblyParityPilots.Load(repo, "26.1.2");
        Assert.Equal(56, pilots.Count);
        Assert.Contains("net.minecraft.client.model.monster.creeper.CreeperModel", pilots);
        Assert.Contains("net.minecraft.client.model.animal.cow.CowModel", pilots);
        Assert.Contains("net.minecraft.client.model.animal.pig.PigModel", pilots);
    }

    [Fact]
    public void Creeper_snapshot_oracle_parses_and_matches_committed_shard_poses()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots",
            "CreeperModel.createBodyLayer.javap.txt");
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.monster.creeper.CreeperModel.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var javapText = File.ReadAllText(snapshotPath);
        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(javapText);
        Assert.True(oracle.Count >= 5, $"expected creeper part poses, got {oracle.Count}");

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, cmp.Message);

        var oracleCtx = GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2");
        Assert.NotNull(oracleCtx);
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(
            "net.minecraft.client.model.monster.creeper.CreeperModel",
            "ok",
            shard.RootElement,
            repo,
            javapPoseOracle: oracleCtx);
        Assert.Equal(true, entry.JavapPoseOracleMatch);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("net.minecraft.client.model.animal.pig.PigModel")]
    [InlineData("net.minecraft.client.model.animal.sheep.SheepModel")]
    [InlineData("net.minecraft.client.model.QuadrupedModel")]
    public void Pilot_jar_oracle_populates_match_field_when_jar_present(string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var jar = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var oracleCtx = GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2");
        Assert.NotNull(oracleCtx);
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvmName, "ok", shard.RootElement, repo, javapPoseOracle: oracleCtx);
        Assert.NotNull(entry.JavapPoseOracleMatch);
        Assert.False(string.IsNullOrWhiteSpace(entry.JavapPoseOracleMessage));
    }

    [Fact]
    public void Write_quality_report_includes_javap_oracle_fields_for_pilots()
    {
        var writePath = Environment.GetEnvironmentVariable("AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY");
        if (string.IsNullOrWhiteSpace(writePath))
        {
            return;
        }

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!Path.IsPathRooted(writePath))
        {
            writePath = Path.Combine(repo, writePath);
        }

        var indexPath = Path.Combine(repo, "docs", "generated", "geometry-index-26.1.2.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var doc = GeometryIrLiftQualityReport.BuildForIndex(repo, "26.1.2", index.RootElement);
        GeometryIrLiftQualityReport.WriteJson(doc, writePath);

        using var written = JsonDocument.Parse(File.ReadAllText(writePath));
        var creeper = written.RootElement.GetProperty("entries").EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("officialJvmName").GetString() ==
                                 "net.minecraft.client.model.monster.creeper.CreeperModel");
        Assert.NotEqual(default, creeper);
        Assert.True(creeper.TryGetProperty("javapPoseOracleMatch", out _));
        Assert.True(creeper.TryGetProperty("javapPoseOracleMessage", out _));
    }

    [Fact]
    public void BuildForIndex_produces_entries_for_ok_shards()
    {
        if (!GeometryIrTestTierSupport.RunLiftQualityIndexDiagnostics())
        {
            return;
        }

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var indexPath = Path.Combine(repo, "docs", "generated", "geometry-index-26.1.2.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var doc = GeometryIrLiftQualityReport.BuildForIndex(repo, "26.1.2", index.RootElement);
        Assert.True(doc.OkEntryCount > 0);
        Assert.NotEmpty(doc.Entries);
    }
}
