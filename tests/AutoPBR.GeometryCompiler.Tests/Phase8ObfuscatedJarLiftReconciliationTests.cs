using System.Text.Json.Nodes;


using Xunit.Abstractions;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Phase 8: live obfuscated jar lift (ProGuard mappings) must match committed 1.21.11 geometry IR roots.
/// </summary>
public sealed class Phase8ObfuscatedJarLiftReconciliationTests(ITestOutputHelper output)
{
    [Theory]
    [MemberData(nameof(Phase8GeometryIrStrictTests.Phase8Cases), MemberType = typeof(Phase8GeometryIrStrictTests))]
    public void Obfuscated_jar_mesh_lift_matches_committed_shard_roots(string officialJvmName)
    {
        var repo = GeometryIrRepoPaths.FindRepoRoot();
        var jar = GeometryIrRepoPaths.ClientJar12111(repo);
        if (jar is null)
        {
            return;
        }

        var javapExe = JavapLocator.FindJavap();
        if (string.IsNullOrWhiteSpace(javapExe))
        {
            return;
        }

        var mapsPath = GeometryIrRepoPaths.Mappings12111(repo);
        Assert.True(File.Exists(mapsPath), $"missing mappings: {mapsPath}");
        var maps = MojangMappingsParser.Load(mapsPath);

        var shardPath = GeometryIrRepoPaths.GeometryShardPath(
            repo,
            GeometryIrRepoPaths.VersionLabel12111,
            officialJvmName);
        Assert.True(File.Exists(shardPath), $"missing shard: {shardPath}");

        var shard = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        Assert.Equal("ok", (string?)shard["extractionStatus"]);
        var factoryMethod = (string?)shard["factoryMethod"] ?? "createBodyLayer";

        Assert.True(
            GeometryLiftPipeline.TryLiftRoots(
                javapExe,
                jar,
                maps,
                officialJvmName,
                factoryMethod,
                out var liftedRoots,
                out var notes),
            string.Join("; ", notes));
        var committedRoots = shard["roots"]!.AsArray();

        var committedJson = GeometryIrRootsCanonicalizer.Canonicalize(committedRoots);
        var liftedJson = GeometryIrRootsCanonicalizer.Canonicalize(liftedRoots);
        if (!string.Equals(committedJson, liftedJson, StringComparison.Ordinal))
        {
            output.WriteLine($"{officialJvmName}: roots JSON mismatch (committed {committedJson.Length} vs lifted {liftedJson.Length} chars)");
        }

        Assert.Equal(committedJson, liftedJson);
    }
}
