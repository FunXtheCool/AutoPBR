using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static partial class GeometryIrLiftQualityReport
{
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
            officialJvmName,
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
}
