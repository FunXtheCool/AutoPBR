using System.Text.Json;
using System.Text.Json.Nodes;


namespace AutoPBR.Tools.GeometryCompiler;

internal sealed partial class GeometryCompilerHost
{
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

        if (GeometryIrDelegatedMeshCopy.HasDelegate(officialJvmName) &&
            GeometryIrDelegatedMeshCopy.TryApply(_outDir, _versionLabel, officialJvmName, json, out _))
        {
            json["schemaVersion"] = 2;
            liftSucceeded = true;
        }
        else if (GeometryLiftPipeline.TryLiftWithJavapFallback(_javap, _clientJar, _maps, officialJvmName, factoryMethod,
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
}
