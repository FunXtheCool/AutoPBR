using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private sealed record PendingPartAttach(string ParentPartId, JsonObject PartNode, string PartName);

    /// <summary>Maps PartDefinition local slots after <c>astore</c> to the part id held there and the receiver slot used at bind time.</summary>
    private sealed record ReceiverSlotEntry(string? PartId, int? ParentReceiverSlot);

    /// <summary>
    /// Void mesh helpers (e.g. <c>createDefaultSkeletonMesh(PartDefinition)</c>) must not use mesh-wide int/float maps from
    /// other islands — cross-island locals can mis-bind part ids and drop cuboids during merge.
    /// </summary>
    internal static bool IslandUsesMeshWideConstantScope(string islandBytecode)
    {
        var hasIslandBoundary = islandBytecode.Contains(
            JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringComparison.Ordinal);
        var delegatesRemoteCreateMesh = islandBytecode.Contains("invokestatic", StringComparison.Ordinal) &&
            islandBytecode.Contains(".createMesh:", StringComparison.Ordinal);
        var hasLocalPartBindings = islandBytecode.Contains("addOrReplaceChild", StringComparison.Ordinal) ||
            islandBytecode.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal);
        if (!hasIslandBoundary && hasLocalPartBindings && delegatesRemoteCreateMesh)
        {
            // e.g. PlayerCapeModel.createCapeLayer: PlayerModel.createMesh prelude + local cape bind — do not
            // merge bipush/int locals from the delegated invoke into the cape addBox segment.
            return false;
        }

        return islandBytecode.Contains("getRoot:", StringComparison.Ordinal) ||
            islandBytecode.Contains("createMesh", StringComparison.Ordinal) ||
            islandBytecode.Contains("createLegs", StringComparison.Ordinal) ||
            islandBytecode.Contains("createBase", StringComparison.Ordinal) ||
            islandBytecode.Contains(":[[I", StringComparison.Ordinal) ||
            islandBytecode.Contains(":[[F", StringComparison.Ordinal);
    }

    internal static bool TryCollectLiftedRootChildren(string meshFactoryJavap, List<string> notes,
        out JsonArray rootChildren, IReadOnlyList<string>? meshWideLines = null)
    {
        rootChildren = new JsonArray();
        var pendingAttaches = new List<PendingPartAttach>();
        var rawLines = meshFactoryJavap.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var foldedLines = JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(rawLines);
        var prologueSkip = FindMeshLiftPrologueSkip(foldedLines);
        var lines = PruneUnreachableMeshFactoryBranches(
            prologueSkip > 0 ? foldedLines.Skip(prologueSkip).ToList() : foldedLines);
        var segmentEnds = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                segmentEnds.Add(i);
            }
        }

        if (segmentEnds.Count == 0)
        {
            if (BytecodeMeshResolution.IslandDefinesPartTreeBindings(meshFactoryJavap))
            {
                notes.Add(
                    "No PartDefinition / PartDefinition-equivalent addChild binding lines found in mesh factory javap.");
            }

            return false;
        }

        var deformInflateByRefSlot = BuildCubeDeformationInflateByRefLocalSlot(lines, _maps);
        var scopeForMeshConstants = meshWideLines is { Count: > 0 } ? meshWideLines : lines;
        var poseFloatLocals = BuildPoseFloatLocalConstantsFromSimpleFstores(lines);
        var boxFloatLocals = poseFloatLocals;
        // Propagate createBodyMesh bipush / createLegs call-site ints from the full concat (other islands).
        var meshWideBoxIntLocals = BuildBoxIntLocalConstants(scopeForMeshConstants);

        var lastClearRecursivelyLine = -1;
        for (var ci = 0; ci < lines.Count; ci++)
        {
            if (lines[ci].Contains("clearRecursively", StringComparison.Ordinal))
            {
                lastClearRecursivelyLine = ci;
            }
        }

        var preserveCuboidsAfterClear = new HashSet<string>(StringComparer.Ordinal);
        _invokeStaticPartNameBindingOrdinal = 0;
        var segStart = 0;
        foreach (var segEnd in segmentEnds)
        {
            var sliceStart = segStart;
            var rawSlice = lines.GetRange(sliceStart, segEnd - sliceStart + 1);
            if (!rawSlice.Any(l => JavapMeshBytecodeProfiles.IsNamedOrObfuscatedFloatAddBoxLine(l, out _)) &&
                !rawSlice.Any(static l =>
                    l.Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                    IsObfuscatedCubeListBuilderCreateLine(l)))
            {
                _ = TryExpandSliceForReusedCubeListBuilder(lines, segEnd, ref sliceStart);
            }

            var seg = lines.GetRange(sliceStart, segEnd - sliceStart + 1);
            segStart = segEnd + 1;
            if (TryFindCountedLoopContaining(lines, segEnd, out var countedLoop) &&
                !SegmentHasExplicitPartNamingForLoopUnroll(seg))
            {
                for (var iter = 0; iter < countedLoop.Limit; iter++)
                {
                    var iterSegBoxInts = BuildBoxIntLocalConstants(seg);
                    var iterBoxInts = WithLoopIteration(
                        MergeBoxIntLocalConstants(iterSegBoxInts, meshWideBoxIntLocals), countedLoop.LoopVarSlot, iter,
                        seg);
                    var iterPoseFloats = new Dictionary<int, double>(poseFloatLocals);
                    ApplySegmentComputedFstorePoseLocals(seg, iterBoxInts, iterPoseFloats);
                    ApplyLoopDerivedPoseFloatLocals(seg, countedLoop.LoopVarSlot, iter, iterPoseFloats);
                    var iterBoxFloats = MergeBoxFloatLocalConstants(boxFloatLocals, iterPoseFloats);
                    if (!TryLiftSegment(seg, deformInflateByRefSlot, iterPoseFloats, iterBoxFloats, iterBoxInts,
                            scopeForMeshConstants, out var loopPartNode, notes))
                    {
                        continue;
                    }

                    if (TryInferLoopPartNameFromSegment(seg, seg.Count - 1, countedLoop.LoopVarSlot, iter,
                            out var loopPartName) && !string.IsNullOrEmpty(loopPartName))
                    {
                        loopPartNode["id"] = loopPartName;
                    }
                    else if (seg.Any(static l => l.Contains("MagmaCubeModel.getSegmentName", StringComparison.Ordinal) ||
                                                  l.Contains("slime/MagmaCubeModel.getSegmentName", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"cube{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("getSegmentName", StringComparison.Ordinal) ||
                                                  l.Contains("createSegmentName", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"segment{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("createTentacleName", StringComparison.Ordinal) ||
                                                  l.Contains("PartNames.tentacle", StringComparison.Ordinal) ||
                                                  l.Contains("/PartNames.tentacle", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"tentacle{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("getNeckName", StringComparison.Ordinal) ||
                                                  l.Contains("neckName:(I)", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"neck{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("tailName:(I)", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"tail{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("boxName:(I)", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"box{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("getCubeName", StringComparison.Ordinal) ||
                                                  l.Contains("createCubeName", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"cube{iter}";
                    }
                    else if (seg.Any(static l => l.Contains("getLayerName", StringComparison.Ordinal)))
                    {
                        loopPartNode["id"] = $"layer{iter}";
                    }
                    else
                    {
                        loopPartName = (string?)loopPartNode["id"];
                    }

                    loopPartName = (string?)loopPartNode["id"];

                    if (string.IsNullOrEmpty(loopPartName))
                    {
                        continue;
                    }

                    AttachLiftedPartToForest(rootChildren, lines, segEnd, loopPartNode,
                        loopPartName, pendingAttaches);
                    FlushPendingPartAttaches(rootChildren, pendingAttaches);
                    if (lastClearRecursivelyLine >= 0 && segEnd > lastClearRecursivelyLine)
                    {
                        preserveCuboidsAfterClear.Add(loopPartName);
                    }
                }

                continue;
            }

            var segPoseFloats = new Dictionary<int, double>(poseFloatLocals);
            var segBoxInts = BuildBoxIntLocalConstants(seg);
            var mergedBoxInts = MergeBoxIntLocalConstants(segBoxInts, meshWideBoxIntLocals);
            ApplySegmentComputedFstorePoseLocals(seg, mergedBoxInts, segPoseFloats);
            var segBoxFloats = MergeBoxFloatLocalConstants(boxFloatLocals, segPoseFloats);
            if (!TryLiftSegment(seg, deformInflateByRefSlot, segPoseFloats, segBoxFloats, mergedBoxInts,
                    scopeForMeshConstants, out var partNode, notes))
            {
                notes.Add($"Mesh segment at bind line {segEnd} skipped: TryLiftSegment returned false.");
                continue;
            }

            var partName = (string?)partNode["id"];
            if (string.IsNullOrEmpty(partName))
            {
                continue;
            }

            AttachLiftedPartToForest(rootChildren, lines, segEnd, partNode, partName,
                pendingAttaches);
            FlushPendingPartAttaches(rootChildren, pendingAttaches);
            if (lastClearRecursivelyLine >= 0 && segEnd > lastClearRecursivelyLine)
            {
                preserveCuboidsAfterClear.Add(partName);
            }
        }

        if (rootChildren.Count == 0)
        {
            return false;
        }

        FlushPendingPartAttaches(rootChildren, pendingAttaches, hoistOrphansToRoot: true);
        // Delegated factories sometimes emit duplicate PartDefinition segments in one island; last segment wins.
        rootChildren = GeometryLiftForestMerge.DeduplicateRootChildrenByPartIdLastWins(rootChildren);
        if (lastClearRecursivelyLine >= 0)
        {
            GeometryLiftForestMerge.ClearCuboidsRecursivelyExcept(rootChildren, preserveCuboidsAfterClear);
        }

        return true;
    }

    internal static void ClearCuboidsRecursively(JsonArray parts) =>
        GeometryLiftForestMerge.ClearCuboidsRecursively(parts);
}
