using AutoPBR.Core.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Scores many shards in one process (regen-assembly-pilots -KeepRevert).</summary>
internal static class BatchScoreLiftShardCommand
{
    public static int Run(string[] args)
    {
        string? manifestPath = null;
        var versionLabel = "26.1.2";
        string? outPath = null;
        var parallel = false;
        var maxParallelism = 0;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            if (string.Equals(a, "--manifest", StringComparison.OrdinalIgnoreCase))
            {
                manifestPath = Next();
            }
            else if (string.Equals(a, "--version-label", StringComparison.OrdinalIgnoreCase))
            {
                versionLabel = Next() ?? versionLabel;
            }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase))
            {
                outPath = Next();
            }
            else if (string.Equals(a, "--parallel", StringComparison.OrdinalIgnoreCase))
            {
                parallel = true;
            }
            else if (string.Equals(a, "--max-parallelism", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(Next(), out var n) && n > 0)
                {
                    maxParallelism = n;
                    parallel = true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            Console.Error.WriteLine(
                "Usage: batch-score-lift-shard --manifest <tsv> [--version-label 26.1.2] [--out <tsv>] [--parallel] [--max-parallelism n]");
            Console.Error.WriteLine("Manifest lines: officialJvmName<TAB>shardPath<TAB>[status]<TAB>[tag]  (# comments allowed)");
            return 2;
        }

        manifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath}");
            return 3;
        }

        var rows = ParseManifest(manifestPath);
        if (rows.Count == 0)
        {
            Console.Error.WriteLine("Manifest has no score rows.");
            return 4;
        }

        var repo = Program.FindRepoRoot();
        var oracle = GeometryJavapPoseOracle.Context.TryCreate(repo, versionLabel);
        var workers = parallel
            ? Math.Max(1, maxParallelism > 0 ? maxParallelism : Math.Min(8, Environment.ProcessorCount))
            : 1;

        LiftShardScore.Result[] results;
        if (workers <= 1)
        {
            results = new LiftShardScore.Result[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                results[i] = LiftShardScore.ScoreFile(repo, row.Jvm, row.ShardPath, row.Status, versionLabel, oracle);
            }
        }
        else
        {
            results = new LiftShardScore.Result[rows.Count];
            Parallel.For(
                0,
                rows.Count,
                new ParallelOptions { MaxDegreeOfParallelism = workers },
                i =>
                {
                    var row = rows[i];
                    results[i] = LiftShardScore.ScoreFile(repo, row.Jvm, row.ShardPath, row.Status, versionLabel, oracle);
                });
        }

        TextWriter writer = Console.Out;
        var ownsWriter = false;
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            outPath = Path.GetFullPath(outPath);
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            writer = new StreamWriter(outPath);
            ownsWriter = true;
        }

        try
        {
            writer.WriteLine(
                "officialJvmName\tshardPath\ttag\tscore\tassemblyGatePass\treferenceWorldPoseMatch\tjavapPoseOracleMatch\textractionBindingGap\tsuspectedFlatNestedPartCount");
            for (var i = 0; i < rows.Count; i++)
            {
                LiftShardScore.WriteTsvRow(writer, rows[i].Jvm, rows[i].ShardPath, rows[i].Tag, results[i]);
            }
        }
        finally
        {
            if (ownsWriter)
            {
                writer.Dispose();
            }
        }

        Console.Error.WriteLine($"batch-score-lift-shard scored {rows.Count} shard(s) workers={workers}");
        return 0;
    }

    private sealed record ManifestRow(string Jvm, string ShardPath, string Status, string? Tag);

    private static List<ManifestRow> ParseManifest(string path)
    {
        var rows = new List<ManifestRow>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                parts = line.Split(',', 3);
            }

            if (parts.Length < 2)
            {
                continue;
            }

            var jvm = parts[0].Trim();
            var shard = parts[1].Trim();
            var status = parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])
                ? parts[2].Trim()
                : "ok";
            if (jvm.Length == 0 || shard.Length == 0)
            {
                continue;
            }

            var tag = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null;
            rows.Add(new ManifestRow(jvm, shard, status, tag));
        }

        return rows;
    }
}
