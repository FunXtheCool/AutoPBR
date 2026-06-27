using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Tools.AnimationCompiler;
using AutoPBR.Tools.GeometryCompiler;
using AutoPBR.Tests.TestSupport;

using GeometryJavapLocator = AutoPBR.Tools.GeometryCompiler.JavapLocator;

using Xunit.Abstractions;

namespace AutoPBR.AnimationCompiler.Tests;

/// <summary>
/// Phase 10: live obfuscated <c>javap -c</c> lift (ProGuard mappings + normalizer) must match committed 1.21.11 animation IR.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class Phase10ObfuscatedAnimationJarLiftReconciliationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions CanonicalJson = new() { WriteIndented = false };

    [Theory]
    [MemberData(nameof(Phase10AnimationIrStrictTests.Phase10Cases), MemberType = typeof(Phase10AnimationIrStrictTests))]
    public void Obfuscated_jar_clinit_lift_matches_committed_shard_definitions(string officialJvmName)
    {
        var repo = AnimationIrRepoPaths.FindRepoRoot();
        var jar = AnimationIrRepoPaths.ClientJar(repo, AnimationIrRepoPaths.VersionLabel12111);
        if (jar is null)
        {
            return;
        }

        var javapExe = GeometryJavapLocator.FindJavap();
        if (string.IsNullOrWhiteSpace(javapExe))
        {
            return;
        }

        var mapsPath = AnimationIrRepoPaths.MappingsPath(repo, AnimationIrRepoPaths.VersionLabel12111);
        Assert.True(File.Exists(mapsPath), $"missing mappings: {mapsPath}");
        var maps = MojangMappingsParser.Load(mapsPath);

        var shardPath = AnimationIrRepoPaths.AnimationShardPath(
            repo,
            officialJvmName,
            AnimationIrRepoPaths.VersionLabel12111);
        Assert.True(File.Exists(shardPath), $"missing shard: {shardPath}");

        var javapArg = maps.TryGetObfuscated(officialJvmName, out var obf)
            ? MojangMappingsParser.GetJavapClassArgForObfuscated(obf)
            : officialJvmName;
        Assert.True(
            JavapRunner.TryDisassemble(javapExe, jar, javapArg, out var javapOut, out var err),
            err ?? "javap disassembly failed");

        var liftSource = AnimationJavapObfuscationNormalizer.Normalize(javapOut, officialJvmName, maps);
        Assert.True(AnimationClinitLift.TryLift(liftSource, out var lifted, out var notes),
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
