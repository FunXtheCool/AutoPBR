using System.Text.Json;
using System.Text.Json.Nodes;


namespace AutoPBR.Tools.GeometryCompiler;

internal sealed partial class GeometryCompilerHost
{
    public int RunBatch(string modelClassListPath, string factoryMethod)
    {
        var lines = File.ReadAllLines(modelClassListPath)
            .Select(l => l.Trim().Replace('\\', '/'))
            .Where(l => l.Length > 0 && l.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[..^".class".Length].Replace('/', '.'))
            .Where(official => !IsPackageInfoStub(official))
            .ToList();

        var mappingKind = _maps is null ? "named_jar" : "proguard";

        if (_emitStats)
        {
            GeometryCompilerStats.Reset();
            GeometryCompilerStats.BeginBatch();
        }

        _batchProgressCompleted = 0;
        var batchStartedUtc = DateTime.UtcNow;
        LogBatchProgress(0, lines.Count, null, null, batchStartedUtc);

        JsonObject[] entryByIndex;
        if (_maxBatchParallelism <= 1)
        {
            entryByIndex = new JsonObject[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                entryByIndex[i] = BuildBatchEntry(lines[i], factoryMethod, mappingKind, lines.Count, batchStartedUtc);
            }
        }
        else
        {
            entryByIndex = new JsonObject[lines.Count];
            var po = new ParallelOptions { MaxDegreeOfParallelism = _maxBatchParallelism };
            Parallel.For(0, lines.Count, po, i =>
            {
                entryByIndex[i] = BuildBatchEntry(lines[i], factoryMethod, mappingKind, lines.Count, batchStartedUtc);
            });
        }

        var indexPath = Path.Combine(_outDir, $"geometry-index-{_versionLabel}.json");
        var mergedByOfficial = LoadIndexEntriesByOfficialName(indexPath);
        foreach (var e in entryByIndex)
        {
            var official = (string?)e["officialJvmName"];
            if (!string.IsNullOrEmpty(official))
            {
                mergedByOfficial[official] = e;
            }
        }

        var entries = new JsonArray();
        foreach (var official in mergedByOfficial.Keys.OrderBy(static s => s, StringComparer.Ordinal))
        {
            entries.Add(JsonNode.Parse(mergedByOfficial[official].ToJsonString())!);
        }

        var index = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = _versionLabel,
            ["mappingKind"] = mappingKind,
            ["clientJarNote"] =
                "Every class in the batch list is written under geometry/<versionLabel>/: jar SHA-256, javap float probe, and (when bytecode matches the mesh factory pattern) a lifted part/cuboid tree from CubeListBuilder.addBox + texOffs + PartPose. Shards are refreshed from bytecode on each run when lift succeeds; alternate mesh hosts (e.g. Adult*Model, Abstract*→concrete) and delegated factories (invokestatic MeshDefinition/LayerDefinition helpers) are resolved automatically.",
            ["entries"] = entries
        };

        File.WriteAllText(indexPath, index.ToJsonString(WriteIndentedJson));
        LogLine($"Wrote {indexPath} ({entryByIndex.Length} updated, {entries.Count} total entries)");

        if (_emitStats)
        {
            var wallBatch = GeometryCompilerStats.EndBatchWallMs();
            var ok = 0;
            var skipped = 0;
            var partial = 0;
            foreach (var e in entryByIndex)
            {
                var s = (string?)e["extractionStatus"];
                if (string.Equals(s, "ok", StringComparison.Ordinal))
                {
                    ok++;
                }
                else if (string.Equals(s, "skipped", StringComparison.Ordinal))
                {
                    skipped++;
                }
                else
                {
                    partial++;
                }
            }

            lock (ConsoleLogLock)
            {
                Console.Error.WriteLine(
                    $"geometry_compiler_stats javap_subprocess_invocations={GeometryCompilerStats.JavapSubprocessInvocations} disasm_cache_hits={GeometryCompilerStats.DisasmCacheHits} wall_batch_ms={wallBatch} parallel={_maxBatchParallelism} entries_ok={ok} entries_skipped={skipped} entries_partial={partial}");
            }
        }

        return 0;
    }

    private JsonObject BuildBatchEntry(string official, string factoryMethod, string mappingKind, int batchTotal,
        DateTime batchStartedUtc)
    {
        var entry = new JsonObject
        {
            ["officialJvmName"] = official,
            ["shardRelPath"] = $"geometry/{_versionLabel}/{official}.json",
            ["profile"] = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}",
            ["modelIndexJvmName"] = official
        };

        var code = ProcessOne(official, factoryMethod, writeIndex: false);
        entry["extractionStatus"] = code switch
        {
            0 => ReadShardStatus(official),
            2 => "skipped",
            _ => "partial"
        };

        if (code == 2)
        {
            entry["extractionNotes"] = new JsonArray(
                "Could not read .class bytes from client.jar (missing entry, mappings mismatch, or inner class path).");
        }
        else if (code != 0)
        {
            entry["extractionNotes"] = new JsonArray("Geometry merge failed; see stderr.");
        }

        if (code == 0)
        {
            TryAddShaFromShard(entry, official);
        }

        var done = Interlocked.Increment(ref _batchProgressCompleted);
        LogBatchProgress(done, batchTotal, official, (string?)entry["extractionStatus"], batchStartedUtc);
        return entry;
    }

    private static void LogBatchProgress(int completed, int total, string? official, string? status, DateTime startedUtc)
    {
        var elapsed = DateTime.UtcNow - startedUtc;
        var rate = completed > 0 ? elapsed.TotalSeconds / completed : 0;
        var eta = completed > 0 && completed < total
            ? TimeSpan.FromSeconds(rate * (total - completed))
            : TimeSpan.Zero;
        var tail = official is null
            ? "starting…"
            : $"{status} {official}";
        lock (ConsoleLogLock)
        {
            Console.Error.WriteLine(
                $"[geometry-index {completed}/{total}] {tail} elapsed={elapsed.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)} eta={(completed > 0 ? eta.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : "?")}");
        }
    }

    private static Dictionary<string, JsonObject> LoadIndexEntriesByOfficialName(string indexPath)
    {
        var map = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (!File.Exists(indexPath))
        {
            return map;
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(indexPath)) is not JsonObject root ||
                root["entries"] is not JsonArray arr)
            {
                return map;
            }

            foreach (var n in arr)
            {
                if (n is not JsonObject o)
                {
                    continue;
                }

                var official = (string?)o["officialJvmName"];
                if (!string.IsNullOrEmpty(official))
                {
                    map[official] = JsonNode.Parse(o.ToJsonString())!.AsObject();
                }
            }
        }
        catch
        {
            // corrupt index — batch run will recreate from processed classes only
        }

        return map;
    }
}
