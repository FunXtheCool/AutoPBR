using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed class GeometryJavapPoseOracleTests
{
    [Fact]
    public void Creeper_snapshot_parses_six_part_poses()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots",
            "CreeperModel.createBodyLayer.javap.txt");
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(File.ReadAllText(snapshotPath));
        Assert.True(
            oracle.Count == 6,
            $"expected 6 parts, got {oracle.Count}: {string.Join(", ", oracle.Keys.OrderBy(static k => k))}");
    }

    [Theory]
    [InlineData("CowModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.cow.CowModel", 6)]
    [InlineData("PigModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.pig.PigModel", 6)]
    [InlineData("SheepModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.sheep.SheepModel", 6)]
    [InlineData("PandaModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.panda.PandaModel", 6)]
    [InlineData("GoatModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.goat.GoatModel", 9)]
    [InlineData("BabyFoxModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.fox.BabyFoxModel", 7)]
    public void Quadruped_pilot_snapshots_parse_six_part_poses(string snapshotFile, string jvmName, int expectedCount)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots", snapshotFile);
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var snapshotDir = Path.GetDirectoryName(snapshotPath)!;
        var javapText = File.ReadAllText(snapshotPath);
        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(javapText, snapshotDir);
        Assert.True(
            oracle.Count == expectedCount,
            $"expected {expectedCount} parts, got {oracle.Count}: {string.Join(", ", oracle.Keys.OrderBy(static k => k))}");
        Assert.Contains("head", oracle.Keys);
        Assert.Contains("body", oracle.Keys);
        if (expectedCount <= 6)
        {
            Assert.DoesNotContain("left_horn", oracle.Keys);
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("AdultFelineModel.createBodyMesh.javap.txt", "net.minecraft.client.model.animal.feline.AdultFelineModel")]
    [InlineData("AdultTurtleModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.turtle.AdultTurtleModel")]
    [InlineData("LlamaModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.llama.LlamaModel")]
    [InlineData("PandaModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("PolarBearModel.createBodyLayer.javap.txt", "net.minecraft.client.model.animal.polarbear.PolarBearModel")]
    [InlineData("EnderDragonModel.createBodyLayer.javap.txt", "net.minecraft.client.model.monster.dragon.EnderDragonModel")]
    public void Pilot_snapshot_oracle_matches_committed_shard(string snapshotFile, string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots", snapshotFile);
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var snapshotDir = Path.GetDirectoryName(snapshotPath)!;
        var javapText = File.ReadAllText(snapshotPath);
        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(javapText, snapshotDir);
        Assert.DoesNotContain("ear2", oracle.Keys);
        Assert.DoesNotContain("goatee", oracle.Keys);
        Assert.DoesNotContain("belly", oracle.Keys);
        Assert.DoesNotContain("nostril", oracle.Keys);

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, $"{jvmName}: {cmp.Message} (oracle parts: {string.Join(", ", oracle.Keys.OrderBy(static k => k))})");
    }

    [Fact]
    public void AdultWolf_snapshot_left_front_leg_matches_bytecode_offsets()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots",
            "AdultWolfModel.createBodyLayer.javap.txt");
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var snapshotDir = Path.GetDirectoryName(snapshotPath)!;
        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(File.ReadAllText(snapshotPath), snapshotDir);
        Assert.True(oracle.TryGetValue("left_front_leg", out var leg), string.Join(", ", oracle.Keys));
        Assert.Equal(0.5, leg.Tx, 2);
        Assert.Equal(16, leg.Ty, 2);
        Assert.Equal(-4, leg.Tz, 2);
    }

    [Theory]
    [InlineData("QuadrupedModel.createBodyMesh.javap.txt", "net.minecraft.client.model.QuadrupedModel", 6)]
    public void Quadruped_base_mesh_snapshot_parses_parametric_head_pose(
        string snapshotFile,
        string jvmName,
        int expectedCount)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var snapshotPath = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "javap-snapshots", snapshotFile);
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var snapshotDir = Path.GetDirectoryName(snapshotPath)!;
        var javapText = File.ReadAllText(snapshotPath);
        var oracle = GeometryJavapPoseOracle.ParseExpectedPosesFromJavap(javapText, snapshotDir);
        Assert.Equal(expectedCount, oracle.Count);
        Assert.True(oracle.TryGetValue("head", out var head));
        Assert.Equal(0, head.Tx, 1);
        Assert.Equal(12, head.Ty, 1);
        Assert.Equal(-6, head.Tz, 1);

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void DonkeyModel_committed_shard_ears_match_createBodyMesh_zero_oracle()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        const string jvm = "net.minecraft.client.model.animal.equine.DonkeyModel";
        var oracleCtx = GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2");
        Assert.NotNull(oracleCtx);
        Assert.True(oracleCtx.TryGetExpectedPoses(jvm, out var oracle, out _));

        Assert.True(oracle.TryGetValue("left_ear", out var leftEar));
        Assert.Equal(0, leftEar.Tx, 3);
        Assert.Equal(0, leftEar.Ty, 3);
        Assert.Equal(0, leftEar.Tz, 3);

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("net.minecraft.client.model.animal.pig.PigModel")]
    [InlineData("net.minecraft.client.model.animal.camel.AdultCamelModel")]
    [InlineData("net.minecraft.client.model.animal.camel.CamelModel")]
    public void Delegate_quadruped_and_camel_pilots_resolve_context_oracle(string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var oracleCtx = GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2");
        Assert.NotNull(oracleCtx);
        Assert.True(
            oracleCtx.TryGetExpectedPoses(jvmName, out var oracle, out var source),
            source ?? "no oracle poses");
        Assert.True(oracle.Count >= 6, $"{jvmName}: expected >=6 parts, got {oracle.Count}");

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
        Assert.True(cmp.IsMatch, $"{jvmName}: {cmp.Message}");
    }

    [Fact]
    public void Assembly_pilot_committed_shards_match_javap_pose_oracle()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var pilots = GeometryAssemblyParityPilots.Load(repo, "26.1.2");
        var oracleCtx = GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2");
        Assert.NotNull(oracleCtx);

        var failures = new List<string>();
        foreach (var jvm in pilots.OrderBy(static j => j, StringComparer.Ordinal))
        {
            var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
            if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
                !string.Equals(status, "ok", StringComparison.Ordinal))
            {
                continue;
            }

            if (!oracleCtx.TryGetExpectedPoses(jvm, out var oracle, out _))
            {
                failures.Add($"{jvm}: no oracle poses");
                continue;
            }

            using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
            var cmp = GeometryJavapPoseOracle.CompareShardToOracle(shard.RootElement, oracle);
            if (!cmp.IsMatch)
            {
                failures.Add($"{jvm}: {cmp.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
