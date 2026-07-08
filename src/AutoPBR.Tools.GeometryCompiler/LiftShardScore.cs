using System.Globalization;
using System.Text.Json;
using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Scores one geometry shard file for keep/revert policy (regen-assembly-pilots).</summary>
internal static class LiftShardScore
{
    internal sealed record Result(
        int Score,
        bool AssemblyGatePass,
        bool? ReferenceWorldPoseMatch,
        bool? JavapPoseOracleMatch,
        bool ExtractionBindingGap,
        int SuspectedFlatNestedPartCount);

    internal static Result ScoreFile(
        string repoRoot,
        string officialJvmName,
        string shardPath,
        string extractionStatus,
        string versionLabel,
        GeometryJavapPoseOracle.Context? javapPoseOracle = null)
    {
        shardPath = Path.GetFullPath(shardPath);
        if (!File.Exists(shardPath))
        {
            throw new FileNotFoundException($"Shard not found: {shardPath}", shardPath);
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(
            officialJvmName,
            extractionStatus,
            shard.RootElement,
            repoRoot,
            javapPoseOracle: javapPoseOracle);
        var score = GeometryIrLiftQualityReport.ComputeLiftDecisionScore(entry);
        return new Result(
            score,
            entry.AssemblyGatePass,
            entry.ReferenceWorldPoseMatch,
            entry.JavapPoseOracleMatch,
            entry.ExtractionBindingGap,
            entry.SuspectedFlatNestedPartCount);
    }

    internal static void WriteTsvRow(
        TextWriter writer,
        string officialJvmName,
        string shardPath,
        string? tag,
        Result result)
    {
        var world = result.ReferenceWorldPoseMatch switch
        {
            true => "True",
            false => "False",
            _ => ""
        };
        var oracle = result.JavapPoseOracleMatch switch
        {
            true => "True",
            false => "False",
            _ => ""
        };
        writer.WriteLine(
            string.Join(
                "\t",
                officialJvmName,
                shardPath,
                tag ?? "",
                result.Score.ToString(CultureInfo.InvariantCulture),
                result.AssemblyGatePass.ToString(),
                world,
                oracle,
                result.ExtractionBindingGap.ToString(),
                result.SuspectedFlatNestedPartCount.ToString(CultureInfo.InvariantCulture)));
    }
}
