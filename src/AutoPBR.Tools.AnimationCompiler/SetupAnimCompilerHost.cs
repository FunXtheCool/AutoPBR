using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.AnimationCompiler;

internal sealed class SetupAnimCompilerHost
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    private static readonly object ConsoleLogLock = new();

    private readonly string _clientJar;
    private readonly string _versionLabel;
    private readonly string _outDir;
    private readonly string? _javap;
    private readonly int _maxBatchParallelism;
    private readonly bool _quiet;
    private readonly bool _emitStats;

    public SetupAnimCompilerHost(string clientJar, string versionLabel, string outDir, string? javapOverride,
        int maxBatchParallelism = 1, bool quiet = false, bool emitStats = false)
    {
        _clientJar = clientJar;
        _versionLabel = versionLabel;
        _outDir = outDir;
        _javap = string.IsNullOrWhiteSpace(javapOverride) ? AnimationJavapLocator.FindJavap() : javapOverride;
        _maxBatchParallelism = Math.Max(1, maxBatchParallelism);
        _quiet = quiet;
        _emitStats = emitStats;
    }

    public int RunSingle(string officialJvmName) => ProcessOne(officialJvmName, writeIndex: true);

    public int RunBatch(string classListPath)
    {
        var lines = File.ReadAllLines(classListPath)
            .Select(l => l.Trim().Replace('\\', '/'))
            .Where(l => l.Length > 0 && l.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[..^".class".Length].Replace('/', '.'))
            .Where(IsModelBatchCandidate)
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
            ["profile"] = $"named_jar_{_versionLabel}",
            ["clientJarNote"] =
                "One row per *Model class with setupAnim lift. Shards under setup-anim/<versionLabel>/.",
            ["entries"] = entries
        };

        var indexPath = Path.Combine(_outDir, $"setup-anim-index-{_versionLabel}.json");
        File.WriteAllText(indexPath, index.ToJsonString(WriteIndentedJson));
        LogLine($"Wrote {indexPath}");

        if (_emitStats)
        {
            var wall = AnimationCompilerStats.EndBatchWallMs();
            lock (ConsoleLogLock)
            {
                Console.Error.WriteLine(
                    $"setup_anim_compiler_stats javap_subprocess_invocations={AnimationCompilerStats.JavapSubprocessInvocations} disasm_cache_hits={AnimationCompilerStats.DisasmCacheHits} wall_batch_ms={wall} parallel={_maxBatchParallelism}");
            }
        }

        return 0;
    }

    private static bool IsModelBatchCandidate(string jvmName)
    {
        if (!jvmName.Contains(".model.", StringComparison.Ordinal))
        {
            return false;
        }

        if (!jvmName.EndsWith("Model", StringComparison.Ordinal))
        {
            return false;
        }

        if (jvmName.Contains('$', StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private JsonObject BuildBatchEntry(string official)
    {
        var entry = new JsonObject
        {
            ["officialJvmName"] = official,
            ["shardRelPath"] = $"setup-anim/{_versionLabel}/{official}.json",
            ["profile"] = $"named_jar_{_versionLabel}"
        };

        var code = ProcessOne(official, writeIndex: false);
        entry["extractionStatus"] = code switch
        {
            0 => ReadShardStatus(official),
            _ => "partial"
        };

        if (code == 2)
        {
            entry["extractionNotes"] = new JsonArray { JsonValue.Create("Could not disassemble or lift setupAnim.") };
        }

        if (code == 0)
        {
            TryAddMetaFromShard(entry, official);
        }

        return entry;
    }

    private int ProcessOne(string entryJvmName, bool writeIndex)
    {
        if (!JavapRunner.TryDisassemble(_javap, _clientJar, entryJvmName, out var javapOut, out var err))
        {
            LogErrorLine(err ?? "javap disassembly failed.");
            return 2;
        }

        if (!ClientJarClassBytes.TryReadClass(_clientJar, entryJvmName, out var classBytes))
        {
            return 2;
        }

        var sha = ClientJarClassBytes.ComputeSha256Hex(classBytes);
        var jarRel = entryJvmName.Replace('.', '/') + ".class";
        var json = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["versionLabel"] = _versionLabel,
            ["officialJvmName"] = entryJvmName,
            ["profile"] = $"named_jar_{_versionLabel}",
            ["jarPath"] = jarRel.Replace('\\', '/'),
            ["classSha256Hex"] = sha
        };

        var lifted = SetupAnimLift.TryLift(javapOut, entryJvmName, out var body, out var liftNotes);
        if (!lifted &&
            SetupAnimLift.TryHoistAbstractHostSetupAnim(_javap!, _clientJar, entryJvmName, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimInheritanceResolver.TryResolveSetupAnimHost(
                _javap!,
                _clientJar,
                entryJvmName,
                javapOut,
                out var hostJvm,
                out _,
                out _) &&
            !string.Equals(hostJvm, entryJvmName, StringComparison.Ordinal) &&
            SetupAnimLift.TryWriteInheritanceOnlyShard(entryJvmName, hostJvm, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimLift.TryWriteNoSetupAnimEffectShard(javapOut, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimLift.TryWriteRendererDrivenSlimeShard(entryJvmName, javapOut, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimLift.TryWriteEntityModelAbstractShard(entryJvmName, javapOut, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimLift.TryWriteModelResetPoseOnlyShard(javapOut, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted &&
            SetupAnimLift.TryWriteInterfaceSetupAnimMarkerShard(entryJvmName, javapOut, out body, out liftNotes))
        {
            lifted = true;
        }

        if (!lifted)
        {
            json["extractionStatus"] = "partial";
            var failNotes = new JsonArray();
            foreach (var n in liftNotes)
            {
                failNotes.Add(JsonValue.Create(n));
            }

            if (failNotes.Count == 0)
            {
                failNotes.Add(JsonValue.Create("SetupAnimLift returned no data."));
            }

            json["extractionNotes"] = failNotes;
        }
        else
        {
            foreach (var kv in body)
            {
                json[kv.Key] = kv.Value?.DeepClone();
            }

            var notes = new JsonArray();
            foreach (var n in liftNotes)
            {
                notes.Add(JsonValue.Create(n));
            }

            var blocking = liftNotes.Count(n => !SetupAnimLift.IsNonBlockingNote(n));
            var hasRules = body["assignments"] is JsonArray { Count: > 0 } ||
                           body["playbackSteps"] is JsonArray { Count: > 0 } ||
                           body["inheritsSetupAnimFrom"] is JsonValue ||
                           body["setupAnimEffectOnly"] is JsonValue;
            json["extractionStatus"] = hasRules && blocking == 0 ? "ok" : "partial";
            if (notes.Count > 0)
            {
                json["extractionNotes"] = notes;
            }

            notes.Add(JsonValue.Create(
                $"SetupAnim IR lifted from javap setupAnim (named_jar_{_versionLabel})."));
            json["extractionNotes"] = notes;
        }

        var outputPath = Path.Combine(_outDir, "setup-anim", _versionLabel, $"{entryJvmName}.json");
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, json.ToJsonString(WriteIndentedJson));
        if (writeIndex)
        {
            UpsertIndexEntry(entryJvmName, jarRel.Replace('\\', '/'), sha, (string?)json["extractionStatus"] ?? "partial");
        }

        LogLine($"Wrote {outputPath}");
        return 0;
    }

    private string ReadShardStatus(string official)
    {
        var p = Path.Combine(_outDir, "setup-anim", _versionLabel, $"{official}.json");
        if (!File.Exists(p))
        {
            return "partial";
        }

        return (string?)JsonNode.Parse(File.ReadAllText(p))!["extractionStatus"] ?? "partial";
    }

    private void TryAddMetaFromShard(JsonObject entry, string official)
    {
        var p = Path.Combine(_outDir, "setup-anim", _versionLabel, $"{official}.json");
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

        if (o["renderStateType"] is JsonValue rs)
        {
            entry["renderStateType"] = rs.ToString();
        }

        if (o["inheritsSetupAnimFrom"] is JsonValue inh)
        {
            entry["inheritsSetupAnimFrom"] = inh.ToString();
        }
    }

    private void UpsertIndexEntry(string official, string jarPath, string sha, string status)
    {
        var indexPath = Path.Combine(_outDir, $"setup-anim-index-{_versionLabel}.json");
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
        entry["shardRelPath"] = $"setup-anim/{_versionLabel}/{official}.json";
        entry["profile"] = $"named_jar_{_versionLabel}";
        entry["extractionStatus"] = status;
        if (existing is null)
        {
            arr.Add(entry);
        }

        File.WriteAllText(indexPath, root.ToJsonString(WriteIndentedJson));
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
}
