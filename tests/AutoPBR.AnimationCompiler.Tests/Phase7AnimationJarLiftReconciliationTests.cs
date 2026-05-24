using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Tools.AnimationCompiler;

using Xunit.Abstractions;

namespace AutoPBR.AnimationCompiler.Tests;

/// <summary>
/// Phase 7: live <c>javap -c</c> lift from pinned client.jar must match committed animation IR structure.
/// </summary>
public sealed class Phase7AnimationJarLiftReconciliationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions CanonicalJson = new() { WriteIndented = false };

    private static string? ClientJarPath
    {
        get
        {
            var repo = AnimationIrRepoPaths.FindRepoRoot();
            var path = Path.Combine(repo, "tools", "minecraft-parity", "26.1.2", "client.jar");
            return File.Exists(path) ? path : null;
        }
    }

    [Theory]
    [MemberData(
        nameof(Phase7AnimationIrStrictTests.Phase7Cases),
        MemberType = typeof(Phase7AnimationIrStrictTests))]
    public void Live_javap_clinit_lift_matches_committed_shard_structure(string officialJvmName)
    {
        var jar = ClientJarPath;
        if (jar is null)
        {
            return;
        }

        var javapExe = JavapLocator.FindJavap();
        if (string.IsNullOrWhiteSpace(javapExe))
        {
            return;
        }

        var repo = AnimationIrRepoPaths.FindRepoRoot();
        var shardPath = AnimationIrRepoPaths.AnimationShardPath(repo, officialJvmName);
        Assert.True(File.Exists(shardPath), $"missing shard: {shardPath}");

        Assert.True(
            JavapRunner.TryDisassemble(javapExe, jar, officialJvmName, out var javapOut, out var err),
            err ?? "javap disassembly failed");
        Assert.True(AnimationClinitLift.TryLift(javapOut, out var lifted, out var notes),
            string.Join("; ", notes));

        var shard = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        Assert.Equal("ok", (string?)shard["extractionStatus"]);
        var committed = shard["definitions"]!.AsArray();

        var committedJson = JsonSerializer.Serialize(committed, CanonicalJson);
        var liftedJson = JsonSerializer.Serialize(lifted, CanonicalJson);
        if (!string.Equals(committedJson, liftedJson, StringComparison.Ordinal))
        {
            var cmp = AnimationIrStructuralComparer.CompareDefinitions(committed, lifted);
            output.WriteLine($"{officialJvmName}: JSON mismatch; structural: {cmp.Message}");
        }

        Assert.Equal(committedJson, liftedJson);
    }
}
