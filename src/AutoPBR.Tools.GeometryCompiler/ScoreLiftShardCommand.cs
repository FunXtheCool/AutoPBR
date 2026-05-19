using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Scores one ok geometry shard for regen-assembly-pilots keep/revert.</summary>
internal static class ScoreLiftShardCommand
{
    public static int Run(string[] args)
    {
        string? jvm = null;
        string? shardPath = null;
        var status = "ok";
        var versionLabel = "26.1.2";

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            if (string.Equals(a, "--jvm", StringComparison.OrdinalIgnoreCase))
            {
                jvm = Next();
            }
            else if (string.Equals(a, "--shard", StringComparison.OrdinalIgnoreCase))
            {
                shardPath = Next();
            }
            else if (string.Equals(a, "--status", StringComparison.OrdinalIgnoreCase))
            {
                status = Next() ?? status;
            }
            else if (string.Equals(a, "--version-label", StringComparison.OrdinalIgnoreCase))
            {
                versionLabel = Next() ?? versionLabel;
            }
        }

        if (string.IsNullOrWhiteSpace(jvm) || string.IsNullOrWhiteSpace(shardPath))
        {
            Console.Error.WriteLine("Usage: score-lift-shard --jvm <fqn> --shard <path> [--status ok] [--version-label 26.1.2]");
            return 2;
        }

        shardPath = Path.GetFullPath(shardPath);
        if (!File.Exists(shardPath))
        {
            Console.Error.WriteLine($"Shard not found: {shardPath}");
            return 3;
        }

        var repo = Program.FindRepoRoot();
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var oracle = GeometryJavapPoseOracle.Context.TryCreate(repo, versionLabel);
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvm, status, shard.RootElement, repo, javapPoseOracle: oracle);
        var score = GeometryIrLiftQualityReport.ComputeLiftDecisionScore(entry);
        Console.WriteLine(
            $"{score}\t{entry.AssemblyGatePass}\t{entry.ReferenceWorldPoseMatch}\t{entry.JavapPoseOracleMatch}\t{entry.ExtractionBindingGap}\t{entry.SuspectedFlatNestedPartCount}");
        return 0;
    }
}
