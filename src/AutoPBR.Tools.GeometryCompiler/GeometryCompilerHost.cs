using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

internal sealed class GeometryCompilerHost
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    private static readonly object ConsoleLogLock = new();

    private int _batchProgressCompleted;

    private readonly string _clientJar;
    private readonly string _versionLabel;
    private readonly string _outDir;
    private readonly string? _javap;
    private readonly MojangMappingsParser? _maps;
    private readonly int _maxBatchParallelism;
    private readonly bool _quiet;
    private readonly bool _emitStats;
    private readonly bool _useAsmLift;
    private readonly bool _compareLift;

    public GeometryCompilerHost(string clientJar, string? mappingsPath, string versionLabel, string outDir,
        string? javapOverride, int maxBatchParallelism = 1, bool quiet = false, bool emitStats = false,
        bool useAsmLift = false, bool compareLift = false)
    {
        _clientJar = clientJar;
        _versionLabel = versionLabel;
        _outDir = outDir;
        _javap = string.IsNullOrWhiteSpace(javapOverride) ? JavapLocator.FindJavap() : javapOverride;
        _maps = string.IsNullOrWhiteSpace(mappingsPath) || !File.Exists(mappingsPath)
            ? null
            : MojangMappingsParser.Load(mappingsPath);
        _maxBatchParallelism = Math.Max(1, maxBatchParallelism);
        _quiet = quiet;
        _emitStats = emitStats;
        _useAsmLift = useAsmLift;
        _compareLift = compareLift;
    }

    public int RunSingle(string officialJvmName, string factoryMethod) =>
        ProcessOne(officialJvmName, factoryMethod, writeIndex: true);

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

    private int ProcessOne(string officialJvmName, string factoryMethod, bool writeIndex)
    {
        if (IsPackageInfoStub(officialJvmName))
        {
            LogLine($"Skipping (Java package-info, not a mesh model): {officialJvmName}");
            return 0;
        }

        var mappingKind = _maps is null ? "named_jar" : "proguard";
        string? obf = null;
        _ = _maps?.TryGetObfuscated(officialJvmName, out obf);

        if (!ClientJarIO.TryResolveJarEntry(_clientJar, officialJvmName, obf, out var jarRel, out var classBytes))
        {
            LogErrorLine($"Could not read class bytes for {officialJvmName} from {_clientJar}.");
            return 2;
        }

        factoryMethod = ResolveFactoryMethodFromMeshHosts(officialJvmName, factoryMethod, classBytes);

        var sha = JvmClassFileParser.ComputeSha256Hex(classBytes);
        var officialJavapArg = obf is null ? officialJvmName : MojangMappingsParser.GetJavapClassArgForObfuscated(obf);

        BytecodeMeshResolution.Result? bytecodeMesh = null;
        if (_useAsmLift &&
            BytecodeMeshResolution.TryResolve(_clientJar, _maps, officialJvmName, factoryMethod, out var br))
        {
            bytecodeMesh = br;
        }

        var meshDisasm = GeometryLiftPipeline.TryResolveMeshDisassembly(_javap, _clientJar, _maps, officialJvmName);
        var hasAsmMesh = !string.IsNullOrEmpty(bytecodeMesh?.MeshConcat);

        var probeJavapArg = meshDisasm?.JavapArg ?? officialJavapArg;
        var precomputedMesh = bytecodeMesh?.MeshConcat ??
                              (meshDisasm is { MeshConcat.Length: > 0 } md ? md.MeshConcat : null);
        var probeStdoutFallback = meshDisasm is not null && meshDisasm.Value.MeshConcat.Length == 0
            ? meshDisasm.Value.Stdout
            : null;

        IReadOnlyList<float> floats;
        bool probeOk;
        if (_useAsmLift && !string.IsNullOrEmpty(precomputedMesh))
        {
            floats = BytecodeFloatProbe.CollectFromSyntheticBytecode(precomputedMesh);
            probeOk = floats.Count > 0;
        }
        else
        {
            probeOk = JavapMethodFloatProbe.TryRun(_javap, _clientJar, probeJavapArg, factoryMethod,
                meshDisasm?.HostJvmName, _maps, probeJavapArg, precomputedMesh, probeStdoutFallback, out floats,
                out var err);
            if (!probeOk)
            {
                LogErrorLine(err ?? "javap float probe failed.");
            }
        }

        var outputShardPath = Path.Combine(_outDir, "geometry", _versionLabel, $"{officialJvmName}.json");
        var useHandTemplate = !_useAsmLift && ShouldUseExistingShardAsMergeTemplate(classBytes, officialJvmName);
        var templatePath = useHandTemplate ? TryPickShardTemplateFile(outputShardPath) : null;
        var json = templatePath is not null
            ? JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject()
            : CreateSyntheticShard(officialJvmName, mappingKind);
        var liftSucceeded = false;

        json["officialJvmName"] = officialJvmName;
        json["versionLabel"] = _versionLabel;
        json["profile"] = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";
        json["factoryMethod"] = factoryMethod;
        json["jarPath"] = jarRel.Replace('\\', '/');
        if (obf is not null)
        {
            json["obfuscatedJvmName"] = obf;
        }
        else
        {
            json.Remove("obfuscatedJvmName");
        }

        GeometryBytecodeMerge.ApplyProbe(json, sha, floats, probeOk);

        if (GeometryLiftPipeline.TryLiftWithJavapFallback(_javap, _clientJar, _maps, officialJvmName, factoryMethod,
                preferAsm: _useAsmLift && hasAsmMesh, out var liftAttempt) &&
            CountAllCuboids(liftAttempt.Roots) > 0)
        {
            var meshText = liftAttempt.MeshConcat;
            var meshHost = liftAttempt.MeshHostJvmName;
            var delegationDepth = meshText.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker).Length - 1;
            json["roots"] = liftAttempt.Roots;
            json["schemaVersion"] = 2;
            if (TryResolveLiftedAtlasDimensions(meshText, meshDisasm, out var texW, out var texH))
            {
                json["textureWidth"] = texW;
                json["textureHeight"] = texH;
            }

            json["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(liftAttempt.Roots, delegationDepth);
            json["extractionStatus"] = "ok";
            var strictValidation = GeometryIrStructuralValidator.ValidateShard(json,
                officialJvmName,
                new GeometryIrStructuralValidator.Options(Strict: true));
            var treeValidation = GeometryIrLiftTreeValidator.ValidateRoots(liftAttempt.Roots, officialJvmName);
            var allLiftIssues = strictValidation.Issues.Concat(treeValidation.Issues).ToList();
            liftSucceeded = strictValidation.IsValid && treeValidation.IsValid;
            liftSucceeded = GeometryIrReferenceBakeGate.Apply(officialJvmName, json, liftSucceeded, allLiftIssues);
            json["extractionStatus"] = liftSucceeded ? "ok" : "partial";

            var liftNotesArr = new JsonArray
            {
                $"Part tree lifted from {liftAttempt.LiftProfile} (mesh host {meshHost})."
            };
            if (!liftSucceeded && allLiftIssues.Count > 0)
            {
                var preview = string.Join("; ",
                    allLiftIssues.Take(4).Select(i => $"{i.Code}: {i.Message}"));
                liftNotesArr.Add($"Strict structural validation failed ({allLiftIssues.Count}): {preview}");
            }

            if (liftAttempt.Notes.Count > 0)
            {
                var preview = string.Join("; ", liftAttempt.Notes.Take(Math.Min(4, liftAttempt.Notes.Count)));
                liftNotesArr.Add($"Mesh lift parser notes ({liftAttempt.Notes.Count}): {preview}");
            }

            json["extractionNotes"] = liftNotesArr;
        }

        ApplyNonMeshShardFinalization(json, classBytes, officialJvmName, liftSucceeded, _maps, factoryMethod);

        var dir = Path.GetDirectoryName(outputShardPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        DedupeExtractionNotes(json);
        if (json["roots"] is JsonArray)
        {
            GeometryIrV2Migration.ApplyToShard(json);
        }

        FinalizeShardExtractionStatus(json, officialJvmName);

        File.WriteAllText(outputShardPath, json.ToJsonString(WriteIndentedJson));
        if (writeIndex)
        {
            UpsertIndexEntry(mappingKind, officialJvmName, jarRel.Replace('\\', '/'), sha,
                (string?)json["extractionStatus"] ?? "partial");
        }

        LogLine($"Wrote {outputShardPath}");
        return 0;
    }

    private static bool TryResolveLiftedAtlasDimensions(
        string? meshText,
        GeometryLiftPipeline.MeshDisassemblyResolution? meshDisasm,
        out int textureWidth,
        out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        if (!string.IsNullOrEmpty(meshText) &&
            LayerDefinitionAtlasSizeProbe.TryRead(meshText, out textureWidth, out textureHeight))
        {
            return true;
        }

        if (meshDisasm is { Stdout.Length: > 0 } disasm &&
            LayerDefinitionAtlasSizeProbe.TryRead(disasm.Stdout, out textureWidth, out textureHeight))
        {
            return true;
        }

        return false;
    }

    private static bool IsPackageInfoStub(string officialJvmName) =>
        officialJvmName.EndsWith(".package-info", StringComparison.Ordinal);

    private static int CountAllCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["cuboids"] is JsonArray c)
            {
                n += c.Count;
            }

            if (p["children"] is JsonArray ch)
            {
                n += CountAllCuboids(ch);
            }
        }

        return n;
    }

    /// <summary>Removes duplicate string entries (e.g. repeated javap probe warnings across regenerations).</summary>
    private static void DedupeExtractionNotes(JsonObject root)
    {
        if (root["extractionNotes"] is not JsonArray a || a.Count < 2)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var next = new JsonArray();
        foreach (var n in a)
        {
            if (n is JsonValue jv && jv.TryGetValue<string>(out var s))
            {
                if (!seen.Add(s))
                {
                    continue;
                }
            }

            next.Add(n!.DeepClone());
        }

        root["extractionNotes"] = next;
    }

    /// <summary>
    /// Picks an on-disk JSON file to use as the merge base, or returns <c>null</c> to synthesize a minimal schema-compliant shard.
    /// </summary>
    private bool ShouldUseExistingShardAsMergeTemplate(ReadOnlySpan<byte> classBytes, string officialJvmName)
    {
        if (officialJvmName.Contains('$', StringComparison.Ordinal) ||
            IsPackageInfoStub(officialJvmName) ||
            JvmClassFileParser.IsInterface(classBytes))
        {
            return false;
        }

        return ProguardMeshFactoryDetection.HasResolvableMeshFactory(_maps, officialJvmName, classBytes);
    }

    private static void FinalizeShardExtractionStatus(JsonObject json, string officialJvmName)
    {
        if (json["roots"] is not JsonArray roots || CountAllCuboids(roots) == 0)
        {
            if (string.Equals((string?)json["extractionStatus"], "heuristic", StringComparison.Ordinal))
            {
                json["extractionStatus"] = "partial";
            }

            return;
        }

        StripHandmadeExtractionNotes(json);
        json["extractionStatus"] = "ok";
        var strictValidation = GeometryIrStructuralValidator.ValidateShard(json,
            officialJvmName,
            new GeometryIrStructuralValidator.Options(Strict: true));
        var treeValidation = GeometryIrLiftTreeValidator.ValidateRoots(roots, officialJvmName);
        var mergeIssues = strictValidation.Issues.Concat(treeValidation.Issues).ToList();
        var ok = strictValidation.IsValid && treeValidation.IsValid;
        ok = GeometryIrReferenceBakeGate.Apply(officialJvmName, json, ok, mergeIssues);
        json["extractionStatus"] = ok ? "ok" : "partial";
    }

    private static void StripHandmadeExtractionNotes(JsonObject json)
    {
        if (json["extractionNotes"] is not JsonArray notes || notes.Count == 0)
        {
            return;
        }

        var kept = new JsonArray();
        foreach (var n in notes)
        {
            if (n is not JsonValue jv || !jv.TryGetValue<string>(out var s))
            {
                kept.Add(n!.DeepClone());
                continue;
            }

            if (s.Contains("CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Derived from CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("temporarily mirrored", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Synced from CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("hand-synced", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("flattened approximations", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            kept.Add(JsonValue.Create(s));
        }

        json["extractionNotes"] = kept;
    }

    private string ResolveFactoryMethodFromMeshHosts(string officialJvmName, string requested,
        ReadOnlySpan<byte> classBytes)
    {
        var resolved = MeshFactoryMethodResolver.Resolve(_maps, officialJvmName, requested, classBytes);
        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            if (!ClientJarIO.TryResolveJarEntry(_clientJar, host, null, out _, out var hostBytes))
            {
                continue;
            }

            if (BytecodeMeshResolution.ShouldSkipMeshHostWithoutPrimaryFactory(host, hostBytes, requested, _maps))
            {
                continue;
            }

            return MeshFactoryMethodResolver.Resolve(_maps, officialJvmName, requested, hostBytes);
        }

        return resolved;
    }

    private void ApplyNonMeshShardFinalization(JsonObject json, ReadOnlySpan<byte> classBytes, string officialJvmName,
        bool liftSucceeded, MojangMappingsParser? maps, string factoryMethod)
    {
        if (liftSucceeded)
        {
            return;
        }

        var hasDelegatedMeshHost = _useAsmLift &&
            BytecodeMeshResolution.TryResolve(_clientJar, maps, officialJvmName, factoryMethod, out _);
        if (JvmClassFileParser.IsInterface(classBytes) ||
            officialJvmName.Contains('$', StringComparison.Ordinal) ||
            IsPackageInfoStub(officialJvmName) ||
            ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(maps, officialJvmName) ||
            (!ProguardMeshFactoryDetection.HasResolvableMeshFactory(maps, officialJvmName, classBytes) &&
             !hasDelegatedMeshHost))
        {
            json["extractionStatus"] = "skipped";
            json["roots"] = CreateSkippedRoots();
            var note = ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(maps, officialJvmName)
                ? $"MeshDefinition transformer on {officialJvmName} (e.g. apply(MeshDefinition)); no static part-tree factory to lift."
                : $"No static mesh factory on {officialJvmName}; structural lift skipped (interface, inner class, or non-mesh type).";
            json["extractionNotes"] = new JsonArray(note);
        }
    }

    private static JsonArray CreateSkippedRoots() =>
        new(new JsonObject
        {
            ["id"] = "root",
            ["pose"] = new JsonObject
            {
                ["translation"] = new JsonArray { 0, 0, 0 },
                ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
                ["eulerOrder"] = "XYZ"
            },
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray()
        });

    private static string? TryPickShardTemplateFile(string outputShardPath)
    {
        if (File.Exists(outputShardPath) && !IsPlaceholderOnlyShard(outputShardPath))
        {
            return outputShardPath;
        }

        return File.Exists(outputShardPath) ? outputShardPath : null;
    }

    private JsonObject CreateSyntheticShard(string officialJvmName, string mappingKind)
    {
        var profile = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";
        return new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = _versionLabel,
            ["officialJvmName"] = officialJvmName,
            ["profile"] = profile,
            ["extractionStatus"] = "partial",
            ["extractionNotes"] = new JsonArray(
                "Synthetic placeholder shard: client.jar metadata (SHA-256, javap float probe) only. Extend GeometryCompiler extraction profiles or re-run with a matching jar so javap mesh lift can emit cuboids."),
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "_placeholder",
                    ["cuboids"] = new JsonArray(),
                    ["children"] = new JsonArray()
                }
            }
        };
    }

    private static bool IsPlaceholderOnlyShard(string path)
    {
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (o["roots"] is not JsonArray roots || roots.Count != 1)
            {
                return false;
            }

            return roots[0] is JsonObject r && string.Equals((string?)r["id"], "_placeholder", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private string ReadShardStatus(string official)
    {
        var shardPath = Path.Combine(_outDir, "geometry", _versionLabel, $"{official}.json");
        if (!File.Exists(shardPath))
        {
            return "partial";
        }

        var o = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        return (string?)o["extractionStatus"] ?? "partial";
    }

    private void TryAddShaFromShard(JsonObject entry, string official)
    {
        var shardPath = Path.Combine(_outDir, "geometry", _versionLabel, $"{official}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        var o = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        if (o["classSha256Hex"] is JsonValue v)
        {
            entry["classSha256Hex"] = v.ToString();
        }

        if (o["jarPath"] is JsonValue jp)
        {
            entry["jarPath"] = jp.ToString();
        }
    }

    private void UpsertIndexEntry(string mappingKind, string official, string jarPath, string sha, string status)
    {
        var indexPath = Path.Combine(_outDir, $"geometry-index-{_versionLabel}.json");
        JsonObject root;
        if (File.Exists(indexPath))
        {
            root = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
        }
        else
        {
            root = new JsonObject
            {
                ["schemaVersion"] = 2,
                ["versionLabel"] = _versionLabel,
                ["mappingKind"] = mappingKind,
                ["entries"] = new JsonArray()
            };
        }

        root["mappingKind"] = mappingKind;
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
        entry["shardRelPath"] = $"geometry/{_versionLabel}/{official}.json";
        entry["profile"] = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";
        entry["extractionStatus"] = status;
        entry["modelIndexJvmName"] = official;
        if (existing is null)
        {
            arr.Add(entry);
        }

        File.WriteAllText(indexPath, root.ToJsonString(WriteIndentedJson));
    }
}
