using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.Tools.AnimationCompiler;

internal sealed class AnimationCompilerHost
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    private static readonly object ConsoleLogLock = new();

    private readonly string _clientJar;
    private readonly MojangMappingsParser? _maps;
    private readonly string _versionLabel;
    private readonly string _outDir;
    private readonly string? _javap;
    private readonly int _maxBatchParallelism;
    private readonly bool _quiet;
    private readonly bool _emitStats;

    public AnimationCompilerHost(string clientJar, string? mappingsPath, string versionLabel, string outDir,
        string? javapOverride, int maxBatchParallelism = 1, bool quiet = false, bool emitStats = false)
    {
        _clientJar = clientJar;
        _maps = string.IsNullOrWhiteSpace(mappingsPath) ? null : MojangMappingsParser.Load(mappingsPath);
        _versionLabel = versionLabel;
        _outDir = outDir;
        _javap = string.IsNullOrWhiteSpace(javapOverride) ? AnimationJavapLocator.FindJavap() : javapOverride;
        _maxBatchParallelism = Math.Max(1, maxBatchParallelism);
        _quiet = quiet;
        _emitStats = emitStats;
    }

    private string ProfileLabel => _maps is null ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";

    public int RunSingle(string officialJvmName) => ProcessOne(officialJvmName, writeIndex: true);

    public int RunBatch(string classListPath)
    {
        var lines = File.ReadAllLines(classListPath)
            .Select(l => l.Trim().Replace('\\', '/'))
            .Where(l => l.Length > 0 && l.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[..^".class".Length].Replace('/', '.'))
            .ToList();

        if (_emitStats)
        {
            AnimationCompilerStats.Reset();
            AnimationCompilerStats.BeginBatch();
        }

        JsonObject[] entryByIndex;
        if (_maxBatchParallelism <= 1)
        {
            entryByIndex = new JsonObject[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                entryByIndex[i] = BuildBatchEntry(lines[i]);
            }
        }
        else
        {
            entryByIndex = new JsonObject[lines.Count];
            var po = new ParallelOptions { MaxDegreeOfParallelism = _maxBatchParallelism };
            Parallel.For(0, lines.Count, po, i => entryByIndex[i] = BuildBatchEntry(lines[i]));
        }

        var entries = new JsonArray();
        foreach (var e in entryByIndex)
        {
            entries.Add(e);
        }

        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["versionLabel"] = _versionLabel,
            ["profile"] = ProfileLabel,
            ["mappingKind"] = _maps is null ? "named_jar" : "proguard",
            ["clientJarNote"] =
                "One row per AnimationDefinition holder class in the batch list. Shards under animation/<versionLabel>/: class SHA-256 and javap-lifted <clinit> channel/keyframe data.",
            ["entries"] = entries
        };

        var indexPath = Path.Combine(_outDir, $"animation-index-{_versionLabel}.json");
        File.WriteAllText(indexPath, index.ToJsonString(WriteIndentedJson));
        LogLine($"Wrote {indexPath}");

        if (_emitStats)
        {
            var wall = AnimationCompilerStats.EndBatchWallMs();
            lock (ConsoleLogLock)
            {
                Console.Error.WriteLine(
                    $"animation_compiler_stats javap_subprocess_invocations={AnimationCompilerStats.JavapSubprocessInvocations} disasm_cache_hits={AnimationCompilerStats.DisasmCacheHits} wall_batch_ms={wall} parallel={_maxBatchParallelism}");
            }
        }

        return 0;
    }

    private JsonObject BuildBatchEntry(string official)
    {
        var entry = new JsonObject
        {
            ["officialJvmName"] = official,
            ["shardRelPath"] = $"animation/{_versionLabel}/{official}.json",
            ["profile"] = ProfileLabel
        };

        var code = ProcessOne(official, writeIndex: false);
        entry["extractionStatus"] = code switch
        {
            0 => ReadShardStatus(official),
            2 => "skipped",
            _ => "partial"
        };

        if (code == 2)
        {
            entry["extractionNotes"] = new JsonArray { JsonValue.Create("Could not read .class bytes from client.jar.") };
        }
        else if (code != 0)
        {
            entry["extractionNotes"] = new JsonArray { JsonValue.Create("javap disassembly failed; see stderr.") };
        }

        if (code == 0)
        {
            TryAddShaFromShard(entry, official);
        }

        return entry;
    }

    private void LogLine(string message)
    {
        if (_quiet)
        {
            return;
        }

        lock (ConsoleLogLock)
        {
            Console.WriteLine(message);
        }
    }

    private static void LogErrorLine(string message)
    {
        lock (ConsoleLogLock)
        {
            Console.Error.WriteLine(message);
        }
    }

    private int ProcessOne(string officialJvmName, bool writeIndex)
    {
        string? obf = null;
        _ = _maps?.TryGetObfuscated(officialJvmName, out obf);
        if (!ClientJarIO.TryResolveJarEntry(_clientJar, officialJvmName, obf, out var jarRel, out var classBytes))
        {
            LogErrorLine($"Could not read class bytes for {officialJvmName} from {_clientJar}.");
            return 2;
        }

        var sha = ClientJarClassBytes.ComputeSha256Hex(classBytes);
        var javapArg = obf is null ? officialJvmName : MojangMappingsParser.GetJavapClassArgForObfuscated(obf);
        if (!JavapRunner.TryDisassemble(_javap, _clientJar, javapArg, out var javapOut, out var err))
        {
            LogErrorLine(err ?? "javap disassembly failed.");
            return 3;
        }

        var json = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["versionLabel"] = _versionLabel,
            ["officialJvmName"] = officialJvmName,
            ["profile"] = ProfileLabel,
            ["jarPath"] = jarRel.Replace('\\', '/'),
            ["classSha256Hex"] = sha,
            ["definitions"] = new JsonArray()
        };
        if (obf is not null)
        {
            json["obfuscatedJvmName"] = obf;
        }

        var liftSource = _maps is null
            ? javapOut
            : AnimationJavapObfuscationNormalizer.Normalize(javapOut, officialJvmName, _maps);
        var declaredCount = CountDeclaredAnimationDefinitionFields(liftSource);
        if (!AnimationClinitLift.TryLift(liftSource, out var definitions, out var liftNotes))
        {
            json["extractionStatus"] = "partial";
            var failNotes = new JsonArray();
            foreach (var n in liftNotes)
            {
                failNotes.Add(JsonValue.Create(n));
            }

            if (failNotes.Count == 0)
            {
                failNotes.Add(JsonValue.Create("AnimationClinitLift returned no definitions."));
            }

            json["extractionNotes"] = failNotes;
        }
        else
        {
            json["definitions"] = definitions;
            var notes = new JsonArray();
            foreach (var n in liftNotes)
            {
                notes.Add(JsonValue.Create(n));
            }

            var partial = liftNotes.Count > 0 ||
                          (declaredCount > 0 && definitions.Count != declaredCount) ||
                          AnimationClinitLift.HasIncompleteChannels(definitions);
            json["extractionStatus"] = partial ? "partial" : "ok";
            if (declaredCount > 0 && definitions.Count != declaredCount)
            {
                notes.Add(JsonValue.Create(
                    $"Declared AnimationDefinition field count ({declaredCount}) differs from lifted putstatic segments ({definitions.Count})."));
            }

            notes.Add(JsonValue.Create(
                $"Animation IR lifted from javap <clinit> ({ProfileLabel}; KeyframeAnimations.degreeVec|posVec|scaleVec FFF/DDD)."));
            json["extractionNotes"] = notes;
        }

        var outputPath = Path.Combine(_outDir, "animation", _versionLabel, $"{officialJvmName}.json");
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, json.ToJsonString(WriteIndentedJson));
        if (writeIndex)
        {
            UpsertIndexEntry(officialJvmName, jarRel.Replace('\\', '/'), sha, (string?)json["extractionStatus"] ?? "partial");
        }

        LogLine($"Wrote {outputPath}");
        return 0;
    }

    private static int CountDeclaredAnimationDefinitionFields(string javapStdout) =>
        Regex.Count(javapStdout,
            @"public\s+static\s+final\s+.*?AnimationDefinition\s+\w+\s*;", RegexOptions.None, TimeSpan.FromSeconds(2));

    private string ReadShardStatus(string official)
    {
        var p = Path.Combine(_outDir, "animation", _versionLabel, $"{official}.json");
        if (!File.Exists(p))
        {
            return "partial";
        }

        var o = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        return (string?)o["extractionStatus"] ?? "partial";
    }

    private void TryAddShaFromShard(JsonObject entry, string official)
    {
        var p = Path.Combine(_outDir, "animation", _versionLabel, $"{official}.json");
        if (!File.Exists(p))
        {
            return;
        }

        var o = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        if (o["classSha256Hex"] is JsonValue v)
        {
            entry["classSha256Hex"] = v.ToString();
        }

        if (o["jarPath"] is JsonValue jp)
        {
            entry["jarPath"] = jp.ToString();
        }
    }

    private void UpsertIndexEntry(string official, string jarPath, string sha, string status)
    {
        var indexPath = Path.Combine(_outDir, $"animation-index-{_versionLabel}.json");
        JsonObject root;
        if (File.Exists(indexPath))
        {
            root = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
        }
        else
        {
            root = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["versionLabel"] = _versionLabel,
                ["profile"] = $"named_jar_{_versionLabel}",
                ["entries"] = new JsonArray()
            };
        }

        if (root["entries"] is not JsonArray arr)
        {
            arr = [];
            root["entries"] = arr;
        }

        JsonObject? existing = null;
        foreach (var n in arr)
        {
            if (n is JsonObject o &&
                string.Equals((string?)o["officialJvmName"], official, StringComparison.Ordinal))
            {
                existing = o;
                break;
            }
        }

        var entry = existing ?? new JsonObject();
        entry["officialJvmName"] = official;
        entry["jarPath"] = jarPath;
        entry["classSha256Hex"] = sha;
        entry["shardRelPath"] = $"animation/{_versionLabel}/{official}.json";
        entry["profile"] = $"named_jar_{_versionLabel}";
        entry["extractionStatus"] = status;
        if (existing is null)
        {
            arr.Add(entry);
        }

        File.WriteAllText(indexPath, root.ToJsonString(WriteIndentedJson));
    }
}
