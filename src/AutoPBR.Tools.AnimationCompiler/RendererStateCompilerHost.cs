using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.AnimationCompiler;

internal sealed class RendererStateCompilerHost
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    private readonly string _clientJar;
    private readonly string _versionLabel;
    private readonly string _outDir;
    private readonly string? _javap;
    private readonly int _maxBatchParallelism;
    private readonly bool _quiet;

    public RendererStateCompilerHost(
        string clientJar,
        string versionLabel,
        string outDir,
        string? javapOverride,
        int maxBatchParallelism = 1,
        bool quiet = false)
    {
        _clientJar = clientJar;
        _versionLabel = versionLabel;
        _outDir = outDir;
        _javap = string.IsNullOrWhiteSpace(javapOverride) ? JavapLocator.FindJavap() : javapOverride;
        _maxBatchParallelism = Math.Max(1, maxBatchParallelism);
        _quiet = quiet;
    }

    public int RunSingle(string officialRendererJvmName) => ProcessOne(officialRendererJvmName, writeIndex: true);

    public int RunBatch(string classListPath)
    {
        var entries = File.ReadAllLines(classListPath)
            .Select(l => l.Trim().Replace('\\', '/'))
            .Where(l => l.Length > 0)
            .Select(l => l.EndsWith(".class", StringComparison.OrdinalIgnoreCase)
                ? l[..^".class".Length].Replace('/', '.')
                : l)
            .Where(IsRendererBatchCandidate)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        JsonObject[] entryByIndex = new JsonObject[entries.Count];
        if (_maxBatchParallelism <= 1)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                entryByIndex[i] = BuildBatchEntry(entries[i]);
            }
        }
        else
        {
            var po = new ParallelOptions { MaxDegreeOfParallelism = _maxBatchParallelism };
            Parallel.For(0, entries.Count, po, i => entryByIndex[i] = BuildBatchEntry(entries[i]));
        }

        var indexEntries = new JsonArray();
        foreach (var entry in entryByIndex)
        {
            indexEntries.Add(entry);
        }

        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["versionLabel"] = _versionLabel,
            ["profile"] = $"named_jar_{_versionLabel}",
            ["clientJarNote"] = "One row per renderer-state MVP lift. Shards under renderer-state/<versionLabel>/.",
            ["entries"] = indexEntries
        };

        var indexPath = Path.Combine(_outDir, $"renderer-state-index-{_versionLabel}.json");
        File.WriteAllText(indexPath, index.ToJsonString(WriteIndentedJson));
        LogLine($"Wrote {indexPath}");
        return 0;
    }

    private JsonObject BuildBatchEntry(string officialRendererJvmName)
    {
        var code = ProcessOne(officialRendererJvmName, writeIndex: false);
        return new JsonObject
        {
            ["officialJvmName"] = officialRendererJvmName,
            ["shardRelPath"] = $"renderer-state/{_versionLabel}/{officialRendererJvmName}.json",
            ["profile"] = $"named_jar_{_versionLabel}",
            ["extractionStatus"] = code == 0 ? "compiler-lift-preview" : "partial"
        };
    }

    private int ProcessOne(string officialRendererJvmName, bool writeIndex)
    {
        if (!JavapRunner.TryDisassemble(_javap, _clientJar, officialRendererJvmName, out var javapOut, out var err))
        {
            LogErrorLine(err ?? "javap disassembly failed.");
            return 2;
        }

        if (!RendererStateLift.TryLift(javapOut, officialRendererJvmName, out var shard, out var notes))
        {
            LogErrorLine(string.Join("; ", notes));
            return 2;
        }

        shard["versionLabel"] = _versionLabel;
        shard["profile"] = $"named_jar_{_versionLabel}";
        shard["jarPath"] = officialRendererJvmName.Replace('.', '/') + ".class";
        if (ClientJarClassBytes.TryReadClass(_clientJar, officialRendererJvmName, out var classBytes))
        {
            shard["classSha256Hex"] = ClientJarClassBytes.ComputeSha256Hex(classBytes);
        }

        var dir = Path.Combine(_outDir, "renderer-state", _versionLabel);
        Directory.CreateDirectory(dir);
        var shardPath = Path.Combine(dir, $"{officialRendererJvmName}.json");
        File.WriteAllText(shardPath, shard.ToJsonString(WriteIndentedJson));
        LogLine($"Wrote {shardPath}");

        if (writeIndex)
        {
            var index = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["versionLabel"] = _versionLabel,
                ["profile"] = $"named_jar_{_versionLabel}",
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["officialJvmName"] = officialRendererJvmName,
                        ["shardRelPath"] = $"renderer-state/{_versionLabel}/{officialRendererJvmName}.json",
                        ["profile"] = $"named_jar_{_versionLabel}",
                        ["extractionStatus"] = "compiler-lift-preview"
                    }
                }
            };
            File.WriteAllText(Path.Combine(_outDir, $"renderer-state-index-{_versionLabel}.json"), index.ToJsonString(WriteIndentedJson));
        }

        return 0;
    }

    private static bool IsRendererBatchCandidate(string jvmName) =>
        jvmName.Contains(".renderer.entity.", StringComparison.Ordinal) &&
        jvmName.EndsWith("Renderer", StringComparison.Ordinal) &&
        !jvmName.Contains('$', StringComparison.Ordinal);

    private void LogLine(string message)
    {
        if (!_quiet)
        {
            Console.Error.WriteLine(message);
        }
    }

    private static void LogErrorLine(string message) => Console.Error.WriteLine(message);
}
