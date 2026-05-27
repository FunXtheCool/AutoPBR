using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static class GeometryIrLiftQualityReport
{
    public sealed record Entry(
        string OfficialJvmName,
        string ExtractionStatus,
        int CuboidCount,
        int MaxTreeDepth,
        int RootChildCount,
        int SuspectedFlatNestedPartCount,
        IReadOnlyDictionary<string, int> LiftWarningCounts,
        bool? ReferenceCuboidsMatch,
        string? ReferenceCompareMessage,
        bool? ReferencePosesMatch,
        string? ReferencePoseCompareMessage,
        bool? ReferenceMeshMatch,
        string? ReferenceMeshCompareMessage,
        bool? ReferenceWorldPoseMatch,
        string? ReferenceWorldPoseCompareMessage,
        bool ReferenceHierarchyMatch,
        string? ReferenceHierarchyMessage,
        bool ExtractionBindingGap,
        bool AssemblyGatePass,
        bool? JavapPoseOracleMatch,
        string? JavapPoseOracleMessage)
    {
        public bool AllReferenceLocalsMatch =>
            ReferenceCuboidsMatch == true &&
            ReferencePosesMatch == true &&
            ReferenceMeshMatch == true;
    }

    /// <summary>Weighted score for keep/revert after pilot re-lift (4A/4B policy).</summary>
    public static int ComputeLiftDecisionScore(Entry entry)
    {
        var score = 0;
        if (entry.AssemblyGatePass)
        {
            score += 1000;
        }

        if (entry.ReferenceWorldPoseMatch == true)
        {
            score += 100;
        }

        if (entry.JavapPoseOracleMatch == true)
        {
            score += 100;
        }

        if (entry.ReferenceHierarchyMatch)
        {
            score += 50;
        }

        if (entry.ReferenceCuboidsMatch == true)
        {
            score += 50;
        }

        if (entry.ExtractionBindingGap)
        {
            score -= 200;
        }

        score -= Math.Min(40, entry.SuspectedFlatNestedPartCount * 5);
        return score;
    }

    public sealed record Document(
        string VersionLabel,
        DateTime GeneratedUtc,
        int OkEntryCount,
        IReadOnlyList<Entry> Entries,
        IReadOnlyList<string> PrioritizedBacklogJvmNames);

    private static readonly HashSet<(string ParentId, string ChildId)> KnownNestedPairs = new()
    {
        ("head", "beak"),
        ("head", "red_thing"),
        ("head", "hat"),
        ("head", "nose"),
        ("head", "mole"),
        ("head", "top_gills"),
        ("head", "left_gills"),
        ("head", "right_gills"),
        ("body", "rods"),
        ("tail1", "tail2"),
        ("body", "left_front_leg"),
        ("body", "right_front_leg"),
        ("body", "left_hind_leg"),
        ("body", "right_hind_leg"),
    };

    private static readonly string[] BodyLegPartIds =
    [
        "left_front_leg",
        "right_front_leg",
        "left_hind_leg",
        "right_hind_leg",
    ];

    public static Document BuildForIndex(
        string repoRoot,
        string versionLabel,
        JsonElement indexRoot,
        IReadOnlySet<string>? referenceCompareJvmNames = null,
        GeometryJavapPoseOracle.Context? javapPoseOracle = null)
    {
        javapPoseOracle ??= GeometryJavapPoseOracle.Context.TryCreate(repoRoot, versionLabel);
        var entries = new List<Entry>();
        if (!indexRoot.TryGetProperty("entries", out var indexEntries) ||
            indexEntries.ValueKind != JsonValueKind.Array)
        {
            return new Document(versionLabel, DateTime.UtcNow, 0, entries, []);
        }

        foreach (var row in indexEntries.EnumerateArray())
        {
            var jvm = row.TryGetProperty("officialJvmName", out var jvmEl) ? jvmEl.GetString() : null;
            if (string.IsNullOrEmpty(jvm))
            {
                continue;
            }

            var status = row.TryGetProperty("extractionStatus", out var stEl) ? stEl.GetString() ?? "" : "";
            if (!string.Equals(status, "ok", StringComparison.Ordinal))
            {
                continue;
            }

            var rel = row.TryGetProperty("shardRelPath", out var relEl)
                ? relEl.GetString()?.Replace('/', Path.DirectorySeparatorChar)
                : null;
            var shardPath = string.IsNullOrEmpty(rel)
                ? null
                : Path.Combine(repoRoot, "docs", "generated", rel!);
            if (shardPath is null || !File.Exists(shardPath))
            {
                continue;
            }

            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            entries.Add(AnalyzeShard(jvm, status, shard.RootElement, repoRoot, referenceCompareJvmNames, javapPoseOracle));
        }

        var backlog = entries
            .Where(e => e.SuspectedFlatNestedPartCount > 0 ||
                        !e.ReferenceHierarchyMatch ||
                        e.ExtractionBindingGap ||
                        !e.AssemblyGatePass ||
                        e.ReferenceCuboidsMatch == false ||
                        e.ReferencePosesMatch == false ||
                        e.ReferenceMeshMatch == false ||
                        e.ReferenceWorldPoseMatch == false ||
                        e.JavapPoseOracleMatch == false ||
                        (e.ReferenceCompareMessage?.Contains("count", StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(e => !e.ReferenceHierarchyMatch || e.ExtractionBindingGap ? 0 : 1)
            .ThenByDescending(e => e.SuspectedFlatNestedPartCount)
            .ThenBy(e => e.ReferenceCuboidsMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferenceWorldPoseMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferencePosesMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferenceMeshMatch == false ? 0 : 1)
            .ThenBy(e => e.JavapPoseOracleMatch == false ? 0 : 1)
            .Select(e => e.OfficialJvmName)
            .ToList();

        return new Document(versionLabel, DateTime.UtcNow, entries.Count, entries, backlog);
    }

    public static Entry AnalyzeShard(
        string officialJvmName,
        string extractionStatus,
        JsonElement shardRoot,
        string repoRoot,
        IReadOnlySet<string>? referenceCompareJvmNames = null,
        GeometryJavapPoseOracle.Context? javapPoseOracle = null)
    {
        var cuboids = 0;
        var maxDepth = 0;
        var rootChildren = 0;
        var flatNested = 0;
        var warnings = new Dictionary<string, int>(StringComparer.Ordinal);

        if (shardRoot.TryGetProperty("roots", out var roots) && roots.ValueKind == JsonValueKind.Array)
        {
            foreach (var root in roots.EnumerateArray())
            {
                if (root.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
                {
                    rootChildren += kids.GetArrayLength();
                    flatNested += CountSuspectedFlatNestedAtRoot(kids);
                }

                WalkMetrics(root, depth: 0, ref cuboids, ref maxDepth, warnings);
            }
        }

        bool? refMatch = null;
        string? refMsg = null;
        bool? refPoseMatch = null;
        string? refPoseMsg = null;
        bool? refMeshMatch = null;
        string? refMeshMsg = null;
        bool? refWorldPoseMatch = null;
        string? refWorldPoseMsg = null;
        bool? referenceLegsAtRoot = null;
        var refPath = Path.Combine(repoRoot, "tools", "MinecraftGeometryReference", "reference-output",
            $"{officialJvmName}.json");
        if (File.Exists(refPath))
        {
            using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
            if (reference.RootElement.TryGetProperty("extractionStatus", out var rst) &&
                string.Equals(rst.GetString(), "reference_java", StringComparison.Ordinal))
            {
                referenceLegsAtRoot = LegsStillRootSiblings(reference.RootElement);

                var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
                    reference.RootElement, shardRoot, tolerance: 0.05);
                refMatch = cmp.IsMatch;
                refMsg = cmp.Message;

                var irForPoseAndMesh = BuildIrRootForReferenceParityCompare(
                    reference.RootElement, shardRoot, officialJvmName);

                if (shardRoot.TryGetProperty("liftSummary", out var liftSummary) &&
                    liftSummary.TryGetProperty("poseApproxCount", out var poseApproxEl) &&
                    poseApproxEl.ValueKind == JsonValueKind.Number &&
                    poseApproxEl.GetInt32() == 0)
                {
                    var poseCmp = GeometryIrReferenceComparer.CompareReferenceToIrShardWithPoses(
                        reference.RootElement, irForPoseAndMesh, cuboidTolerance: 0.05, poseTolerance: 0.05);
                    refPoseMatch = poseCmp.IsMatch;
                    refPoseMsg = poseCmp.Message;
                }

                if (TryCompareReferenceParityMesh(
                        reference.RootElement,
                        irForPoseAndMesh,
                        officialJvmName,
                        out var meshCmp))
                {
                    refMeshMatch = meshCmp.IsMatch;
                    refMeshMsg = meshCmp.Message;
                }

                (refWorldPoseMatch, refWorldPoseMsg) = CompareReferenceWorldPoses(
                    reference.RootElement, shardRoot, officialJvmName);
            }
        }
        else if (referenceCompareJvmNames?.Contains(officialJvmName) == true)
        {
            refMatch = false;
            refMsg = "reference json missing";
        }

        var extractionNotes = ReadExtractionNotes(shardRoot);
        var bindingGap = HasExtractionMissingAddChildNote(extractionNotes);
        var legsAtRoot = LegsStillRootSiblings(shardRoot);
        var bindingNote = HasAddChildBindingExtractionNote(extractionNotes);
        (var referenceHierarchyMatch, string? hierarchyMsg) = EvaluateReferenceHierarchyMatch(
            shardRoot,
            flatNested,
            legsAtRoot,
            bindingNote,
            referenceLegsAtRoot);
        bool? javapOracleMatch = null;
        string? javapOracleMsg = null;
        if (javapPoseOracle?.IsPilot(officialJvmName) == true)
        {
            if (javapPoseOracle.TryGetExpectedPoses(officialJvmName, out var oraclePoses, out var oracleSource))
            {
                var oracleCmp = GeometryJavapPoseOracle.CompareShardToOracle(shardRoot, oraclePoses);
                javapOracleMatch = oracleCmp.IsMatch;
                javapOracleMsg = oracleCmp.Message ?? oracleSource;
            }
            else
            {
                javapOracleMsg = oracleSource ?? "javap pose oracle unavailable";
            }
        }

        var assemblyGatePass = referenceHierarchyMatch &&
                                 !bindingGap &&
                                 refMatch != false &&
                                 refPoseMatch != false &&
                                 refMeshMatch != false &&
                                 refWorldPoseMatch != false &&
                                 javapOracleMatch != false;

        return new Entry(
            officialJvmName,
            extractionStatus,
            cuboids,
            maxDepth,
            rootChildren,
            flatNested,
            warnings,
            refMatch,
            refMsg,
            refPoseMatch,
            refPoseMsg,
            refMeshMatch,
            refMeshMsg,
            refWorldPoseMatch,
            refWorldPoseMsg,
            referenceHierarchyMatch,
            hierarchyMsg,
            bindingGap,
            assemblyGatePass,
            javapOracleMatch,
            javapOracleMsg);
    }

    private static List<string> ReadExtractionNotes(JsonElement shardRoot)
    {
        var notes = new List<string>();
        if (!shardRoot.TryGetProperty("extractionNotes", out var notesEl) ||
            notesEl.ValueKind != JsonValueKind.Array)
        {
            return notes;
        }

        foreach (var note in notesEl.EnumerateArray())
        {
            var text = note.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                notes.Add(text);
            }
        }

        return notes;
    }

    private static bool HasAddChildBindingExtractionNote(IReadOnlyList<string> extractionNotes) =>
        extractionNotes.Any(IndicatesAddChildHierarchyBindingNote);

    private static bool HasExtractionMissingAddChildNote(IReadOnlyList<string> extractionNotes) =>
        extractionNotes.Any(IndicatesAddChildExtractionBindingGap);

    /// <summary>
    /// Lift parser logs "No PartDefinition … addChild binding lines found" when javap uses flat
    /// addOrReplaceChild only — not a hierarchy lift failure.
    /// </summary>
    private static bool IndicatesAddChildHierarchyBindingNote(string note) =>
        note.Contains("addChild binding", StringComparison.OrdinalIgnoreCase) &&
        !note.Contains("No PartDefinition", StringComparison.OrdinalIgnoreCase) &&
        !note.Contains("no addChild binding lines found", StringComparison.OrdinalIgnoreCase);

    private static bool IndicatesAddChildExtractionBindingGap(string note) =>
        note.Contains("missing addChild", StringComparison.OrdinalIgnoreCase) ||
        IndicatesAddChildHierarchyBindingNote(note);

    private static bool LegsStillRootSiblings(JsonElement shardRoot)
    {
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var rootChildIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ch in children.EnumerateArray())
            {
                if (ch.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        rootChildIds.Add(id);
                    }
                }
            }

            if (!rootChildIds.Contains("body"))
            {
                continue;
            }

            if (BodyLegPartIds.Any(rootChildIds.Contains))
            {
                return true;
            }
        }

        return false;
    }

    private static (bool Match, string? Message) EvaluateReferenceHierarchyMatch(
        JsonElement shardRoot,
        int flatNested,
        bool legsAtRoot,
        bool bindingNote,
        bool? referenceLegsAtRoot)
    {
        if (referenceLegsAtRoot == true && LegsNestedUnderBody(shardRoot))
        {
            return (false, "IR nests legs under body but reference_java uses vanilla flat root siblings");
        }

        if (flatNested > 0 && legsAtRoot)
        {
            if (referenceLegsAtRoot == false)
            {
                if (bindingNote)
                {
                    return (false, "extractionNotes mention addChild binding; hierarchy not lifted");
                }

                // Composed-flat: IR mirrors flat Java factory; reference_java nests legs (world pose via topology align).
                return (true, null);
            }

            if (referenceLegsAtRoot != true)
            {
                return (false,
                    $"suspectedFlatNestedPartCount={flatNested} with body/legs still root siblings (reference unavailable)");
            }

            if (!TryGetFirstRootChildren(shardRoot, out var irRootKids) ||
                !GeometryIrPartTreeRepair.UsesVanillaFlatQuadrupedLegBake(irRootKids))
            {
                return (false,
                    $"suspectedFlatNestedPartCount={flatNested} with body/legs still root siblings (not vanilla flat quadruped layout)");
            }

            return (true, null);
        }

        if (bindingNote)
        {
            return (false, "extractionNotes mention addChild binding; hierarchy not lifted");
        }

        if (flatNested > 0)
        {
            return (false, $"suspectedFlatNestedPartCount={flatNested}");
        }

        return (true, null);
    }

    private static bool TryGetFirstRootChildren(JsonElement shardRoot, out JsonArray rootChildren)
    {
        rootChildren = [];
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var node = JsonNode.Parse(children.GetRawText());
            if (node is JsonArray arr)
            {
                rootChildren = arr;
                return true;
            }
        }

        return false;
    }

    private static bool LegsNestedUnderBody(JsonElement shardRoot)
    {
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!TryFindPartById(root, "body", out var body))
            {
                continue;
            }

            if (!body.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var legId in BodyLegPartIds)
            {
                foreach (var ch in children.EnumerateArray())
                {
                    if (ch.TryGetProperty("id", out var idEl) &&
                        string.Equals(idEl.GetString(), legId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryFindPartById(JsonElement node, string id, out JsonElement found)
    {
        if (node.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), id, StringComparison.Ordinal))
        {
            found = node;
            return true;
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                if (TryFindPartById(ch, id, out found))
                {
                    return true;
                }
            }
        }

        found = default;
        return false;
    }

    private static int CountSuspectedFlatNestedAtRoot(JsonElement rootChildren)
    {
        var rootIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ch in rootChildren.EnumerateArray())
        {
            if (ch.TryGetProperty("id", out var idEl))
            {
                var id = idEl.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    rootIds.Add(id);
                }
            }
        }

        var n = 0;
        foreach (var (parent, child) in KnownNestedPairs)
        {
            if (rootIds.Contains(child) && rootIds.Contains(parent))
            {
                n++;
            }
        }

        return n;
    }

    private static void WalkMetrics(
        JsonElement part,
        int depth,
        ref int cuboids,
        ref int maxDepth,
        Dictionary<string, int> warnings)
    {
        maxDepth = Math.Max(maxDepth, depth);
        if (part.TryGetProperty("cuboids", out var cuboidArr) && cuboidArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cuboidArr.EnumerateArray())
            {
                cuboids++;
                if (c.TryGetProperty("liftWarnings", out var wArr) && wArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in wArr.EnumerateArray())
                    {
                        var code = w.GetString();
                        if (string.IsNullOrEmpty(code))
                        {
                            continue;
                        }

                        warnings[code] = warnings.GetValueOrDefault(code) + 1;
                    }
                }
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkMetrics(ch, depth + 1, ref cuboids, ref maxDepth, warnings);
            }
        }
    }

    private static (bool? Match, string? Message) CompareReferenceWorldPoses(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName)
    {
        var repairedIr = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        var repairedCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, repairedIr, tolerance: 0.05);
        if (repairedCmp.IsMatch)
        {
            return (true, null);
        }

        var topologyAligned = GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(referenceRoot, repairedIr);
        var poseSynced = GeometryIrReferencePoseSync.ApplyForComparisons(referenceRoot, topologyAligned);
        var alignedCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, poseSynced, tolerance: 0.05);
        if (alignedCmp.IsMatch)
        {
            return (true, null);
        }

        var rawCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, shardRoot, tolerance: 0.05);
        var message = alignedCmp.Message ?? repairedCmp.Message;
        if (rawCmp.IsMatch)
        {
            message = string.IsNullOrEmpty(message)
                ? "raw IR tree world pose match; parity-repaired tree diverges"
                : $"{message}; raw IR tree world pose match";
        }

        return (false, message);
    }

    private static JsonElement BuildIrRootForReferenceParityCompare(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName)
    {
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        if (!shardRoot.TryGetProperty("liftSummary", out var liftSummary) ||
            !liftSummary.TryGetProperty("poseApproxCount", out var poseApproxEl) ||
            poseApproxEl.ValueKind != JsonValueKind.Number ||
            poseApproxEl.GetInt32() != 0)
        {
            return repaired;
        }

        return GeometryIrReferencePoseSync.ApplyForComparisons(referenceRoot, repaired);
    }

    private static bool TryCompareReferenceParityMesh(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName,
        out GeometryIrReferenceComparer.CompareResult cmp)
    {
        cmp = default;
        if (!shardRoot.TryGetProperty("extractionStatus", out var st) ||
            !string.Equals(st.GetString(), "ok", StringComparison.Ordinal))
        {
            return false;
        }

        var (atlasW, atlasH) = ResolveParityAtlasForQualityReport(officialJvmName);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", profile, officialJvmName, atlasW, atlasH, out _, shardRoot);
        if (mesh is null)
        {
            return false;
        }

        cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(referenceRoot, mesh, tolerance: 0.08);
        return true;
    }

    private static (int W, int H) ResolveParityAtlasForQualityReport(string officialJvmName) =>
        officialJvmName switch
        {
            "net.minecraft.client.model.animal.fish.CodModel" => (32, 32),
            "net.minecraft.client.model.animal.fish.SalmonModel" => (32, 32),
            "net.minecraft.client.model.animal.chicken.ChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.chicken.BabyChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.cow.CowModel" => (64, 64),
            "net.minecraft.client.model.animal.cow.ColdCowModel" => (64, 64),
            "net.minecraft.client.model.animal.pig.PigModel" => (64, 64),
            "net.minecraft.client.model.ambient.BatModel" => (64, 64),
            "net.minecraft.client.model.monster.creeper.CreeperModel" => (64, 32),
            _ => (64, 64)
        };

    public static void WriteJson(Document doc, string outputPath)
    {
        var entries = new List<object>();
        foreach (var e in doc.Entries)
        {
            entries.Add(new
            {
                officialJvmName = e.OfficialJvmName,
                extractionStatus = e.ExtractionStatus,
                cuboidCount = e.CuboidCount,
                maxTreeDepth = e.MaxTreeDepth,
                rootChildCount = e.RootChildCount,
                suspectedFlatNestedPartCount = e.SuspectedFlatNestedPartCount,
                liftWarningCounts = e.LiftWarningCounts,
                referenceCuboidsMatch = e.ReferenceCuboidsMatch,
                referenceCompareMessage = e.ReferenceCompareMessage,
                referencePosesMatch = e.ReferencePosesMatch,
                referencePoseCompareMessage = e.ReferencePoseCompareMessage,
                referenceMeshMatch = e.ReferenceMeshMatch,
                referenceMeshCompareMessage = e.ReferenceMeshCompareMessage,
                referenceWorldPoseMatch = e.ReferenceWorldPoseMatch,
                referenceWorldPoseCompareMessage = e.ReferenceWorldPoseCompareMessage,
                referenceHierarchyMatch = e.ReferenceHierarchyMatch,
                referenceHierarchyMessage = e.ReferenceHierarchyMessage,
                extractionBindingGap = e.ExtractionBindingGap,
                assemblyGatePass = e.AssemblyGatePass,
                javapPoseOracleMatch = e.JavapPoseOracleMatch,
                javapPoseOracleMessage = e.JavapPoseOracleMessage
            });
        }

        var root = new
        {
            schemaVersion = 2,
            versionLabel = doc.VersionLabel,
            generatedUtc = doc.GeneratedUtc.ToString("O"),
            okEntryCount = doc.OkEntryCount,
            prioritizedBacklogJvmNames = doc.PrioritizedBacklogJvmNames,
            entries
        };

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
    }
}
