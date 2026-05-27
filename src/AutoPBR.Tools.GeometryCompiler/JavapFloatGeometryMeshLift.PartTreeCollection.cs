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

    /// <summary>
    /// Maps local slots that hold a <c>PartDefinition</c> receiver to the mesh part id they represent for nested
    /// <c>addOrReplaceChild</c> calls (humanoid, quadruped, aquatic, etc.). <c>null</c> means the mesh root from <c>getRoot</c>.
    /// </summary>
    /// <remarks>
    /// Concatenated <c>javap -c</c> blocks may reuse local indices across methods; callers that concatenate unrelated methods
    /// should split on <see cref="JavapClassDisassembly.GeometryMeshIslandBoundaryMarker"/> (or equivalent) so each slice
    /// gets a fresh map from this method.
    /// </remarks>
    private static Dictionary<int, ReceiverSlotEntry> BuildReceiverLocalSlotGraph(List<string> lines)
    {
        var map = new Dictionary<int, ReceiverSlotEntry>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapBytecodeStreamAnalyzer.IsJavapMethodCodeHeaderLine(lines[i]))
            {
                map.Clear();
                continue;
            }

            if (IsMeshRootGetRootInvokeLine(lines[i]) &&
                TryFindFirstAstoreLocalSlotAfter(lines, i, 1, 10, out _, out var rootSlot))
            {
                map[rootSlot] = new ReceiverSlotEntry(null, null);
            }

            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                continue;
            }

            var head = lines.GetRange(0, i + 1);
            var child = FindPartNameNearAddOrReplaceChild(head, new Dictionary<int, int>()) ??
                        FindLdcPartNameImmediatelyBeforeBinding(lines, i);
            if (string.IsNullOrEmpty(child))
            {
                continue;
            }

            var parentReceiverSlot = TryInferReceiverLocalSlotForBinding(lines, i, child);
            if (TryFindFirstAstoreLocalSlotAfter(lines, i, 1, 256, out _, out var chainSlot))
            {
                map[chainSlot] = new ReceiverSlotEntry(child, parentReceiverSlot);
            }
        }

        return map;
    }

    /// <summary>
    /// Tracks <c>Reference</c>-typed locals holding <c>CubeDeformation</c> assigned from <c>new CubeDeformation(f)</c>,
    /// <c>getstatic … ZERO</c>, or copies <c>aload→astore</c>, so <c>aload</c> before <c>addBox(…CubeDeformation;)</c> can resolve inflate.
    /// </summary>
    private static Dictionary<int, double> BuildCubeDeformationInflateByRefLocalSlot(List<string> lines,
        MojangMappingsParser? maps)
    {
        var map = new Dictionary<int, double>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            var winLow = Math.Max(0, i - 24);
            var prev = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, i - 1);
            if (prev.Contains("getstatic", StringComparison.Ordinal) &&
                (prev.Contains("CubeDeformation", StringComparison.Ordinal) ||
                 (maps is not null && IsObfuscatedCubeDeformationGetStatic(prev, maps))) &&
                (prev.Contains("ZERO", StringComparison.Ordinal) || prev.Contains("NONE", StringComparison.Ordinal) ||
                 prev.Contains(".a:L", StringComparison.Ordinal)))
            {
                map[slot] = 0;
                continue;
            }

            if (TryReadUniformCubeDeformationCtorInvokespecialLine(lines, i - 1, winLow, maps, out var inf))
            {
                map[slot] = inf;
            }
        }

        for (var iter = 0; iter < 8; iter++)
        {
            for (var i = 0; i < lines.Count - 1; i++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[i], out var a) || !JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i + 1], out var b))
                {
                    continue;
                }

                if (map.TryGetValue(a, out var v))
                {
                    map[b] = v;
                }
            }
        }

        ApplyCubeDeformationExtendAssignments(lines, map, maps);

        return map;
    }

    /// <summary>
    /// <c>aload base; ldc delta; invokevirtual CubeDeformation.extend:(F)...; astore dest</c> → dest inflate = base + delta.
    /// </summary>
    private static void ApplyCubeDeformationExtendAssignments(List<string> lines, Dictionary<int, double> map,
        MojangMappingsParser? maps)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i], out var destSlot))
            {
                continue;
            }

            if (!TryResolveCubeDeformationExtendInflate(lines, i, map, maps, out var inflate))
            {
                continue;
            }

            map[destSlot] = inflate;
        }
    }

    private static bool TryResolveCubeDeformationExtendInflate(List<string> lines, int astoreLineIdx,
        IReadOnlyDictionary<int, double> map, MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        for (var d = 1; d <= 8 && astoreLineIdx - d >= 0; d++)
        {
            var ln = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, astoreLineIdx - d);
            if (!IsCubeDeformationExtendInvokeLine(ln, maps))
            {
                continue;
            }

            if (!TryParseExtendDeltaBackward(lines, astoreLineIdx - d, out var delta))
            {
                return false;
            }

            for (var e = d + 1; e <= d + 6 && astoreLineIdx - e >= 0; e++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[astoreLineIdx - e], out var baseSlot))
                {
                    continue;
                }

                if (!map.TryGetValue(baseSlot, out var baseInflate))
                {
                    return false;
                }

                inflate = baseInflate + delta;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsCubeDeformationExtendInvokeLine(string line, MojangMappingsParser? maps)
    {
        if (!line.Contains("invokevirtual", StringComparison.Ordinal) ||
            !line.Contains("extend", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains("CubeDeformation", StringComparison.Ordinal) &&
            line.Contains("(F)", StringComparison.Ordinal))
        {
            return true;
        }

        return maps is not null &&
               maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf) &&
               JavapBytecodeStreamAnalyzer.ObfJavapLineReferencesShortType(line, MojangMappingsParser.GetJavapClassArgForObfuscated(obf)) &&
               line.Contains("extend", StringComparison.Ordinal);
    }

    private static bool TryParseExtendDeltaBackward(List<string> lines, int extendLineIdx, out double delta)
    {
        delta = 0;
        for (var d = 1; d <= 4 && extendLineIdx - d >= 0; d++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[extendLineIdx - d], out delta))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>ldc</c>/<c>fconst_*</c>/<c>fneg</c> + <c>fstore</c> patterns so <c>PartPose</c> stacks can resolve <c>fload</c> to a constant.
    /// </summary>
    private static Dictionary<int, double> BuildPoseFloatLocalConstantsFromSimpleFstores(IReadOnlyList<string> lines)
    {
        var map = new Dictionary<int, double>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[i - 1], out var fv))
            {
                map[slot] = fv;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.JavapI2fInsnRegex().IsMatch(lines[i - 1]) && i >= 2 && JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[i - 2], out var iv))
            {
                map[slot] = iv;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.JavapFnegInsnRegex().IsMatch(lines[i - 1]) && i >= 2 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[i - 2], out var inner))
            {
                map[slot] = -inner;
            }
        }

        PropagatePoseFloatLocalCopies(lines, map);
        ApplyMeshFactoryFloatParamFromCallSites(lines, map);
        ApplyMeshFactoryFloatParamDefaultForCreateMesh(lines, map);
        return map;
    }

    /// <summary>
    /// <c>createMesh(CubeDeformation, float)</c> uses parameter slot 1 in <c>PartPose.offset</c> via <c>fload_1</c>.
    /// Default <c>0</c> matches <c>GeometryReferenceBake.invokeWithDefaults</c> (reference_java bakes).
    /// </summary>
    private static void ApplyMeshFactoryFloatParamDefaultForCreateMesh(IReadOnlyList<string> lines, Dictionary<int, double> map)
    {
        if (map.ContainsKey(1))
        {
            return;
        }

        if (!UsesMeshFactoryFloatParamInPartPoseOffsets(lines))
        {
            return;
        }

        map[1] = 0.0;
    }

    private static bool UsesMeshFactoryFloatParamInPartPoseOffsets(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("PartPose.offset", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = i - 1; j >= 0 && j > i - 32; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[j], out var slot) && slot == 1)
                {
                    return true;
                }
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[i], out var slot) && slot == 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves <c>fload</c> slots used as the float argument to <c>createMesh(CubeDeformation, float)</c> from call sites in the same concat.
    /// </summary>
    private static void ApplyMeshFactoryFloatParamFromCallSites(IReadOnlyList<string> lines, Dictionary<int, double> map)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("createMesh", StringComparison.Ordinal) ||
                (!line.Contains(";F)", StringComparison.Ordinal) && !line.Contains("(F)", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!TryExtractBackwardFloatConstant(lines, i - 1, map, out var meshHeight))
            {
                continue;
            }

            map[1] = meshHeight;
        }
    }

    private static void PropagatePoseFloatLocalCopies(IReadOnlyList<string> lines, Dictionary<int, double> map)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            var changed = false;
            for (var i = 1; i < lines.Count; i++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(lines[i], out var dst) ||
                    !JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[i - 1], out var src) ||
                    !map.TryGetValue(src, out var v))
                {
                    continue;
                }

                if (!map.TryGetValue(dst, out var existing) || Math.Abs(existing - v) > 1e-9)
                {
                    map[dst] = v;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool TryExtractBackwardFloatConstant(IReadOnlyList<string> lines, int startIdx,
        IReadOnlyDictionary<int, double> floatLocals, out double value)
    {
        value = 0;
        for (var j = startIdx; j >= 0; j--)
        {
            if (lines[j].Contains("invokestatic", StringComparison.Ordinal) ||
                lines[j].Contains("invokevirtual", StringComparison.Ordinal) ||
                lines[j].Contains("invokespecial", StringComparison.Ordinal))
            {
                break;
            }

            if (JavapBytecodeStreamAnalyzer.IsBackwardStackNoiseLine(lines[j]) ||
                lines[j].Contains("iconst_", StringComparison.Ordinal))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[j], out var fv))
            {
                value = fv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.JavapI2fInsnRegex().IsMatch(lines[j]) && j >= 1 && JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j - 1], out var iv))
            {
                value = iv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[j], out var slot) && floatLocals.TryGetValue(slot, out var loaded))
            {
                value = loaded;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves simple <c>istore</c> constants and quadruped leg-scale ints propagated from
    /// <c>createBodyMesh</c> / <c>createLegs</c> call sites in concatenated mesh factories.
    /// </summary>
    private static Dictionary<int, int> BuildBoxIntLocalConstants(IReadOnlyList<string> lines)
    {
        var map = BuildBoxIntLocalConstantsFromSimpleIstores(lines);
        ApplyMeshFactoryBodySizeFromCallSites(lines, map);
        ApplyQuadrupedLegScaleFromCallSites(lines, map);
        return map;
    }

    internal static IReadOnlyDictionary<int, int> BuildBoxIntLocalConstantsForTests(IReadOnlyList<string> lines) =>
        BuildBoxIntLocalConstants(lines);

    /// <summary>
    /// Drops <c>MeshDefinition</c> construction and <c>getRoot</c> receiver setup. Some factories (FrogModel) then
    /// attach an empty <c>root</c> wrapper before real parts; that block poisons segment lift and is skipped through the
    /// first <c>CubeListBuilder.create</c>. Others (AdultAxolotlModel) ldc the first real part name before create on the
    /// mesh root — keep those lines so the body part id and cuboids are not lost.
    /// </summary>
    private static int FindMeshLiftPrologueSkip(IReadOnlyList<string> lines)
    {
        var getRootLine = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsMeshRootGetRootInvokeLine(lines[i]))
            {
                continue;
            }

            getRootLine = i;
            break;
        }

        if (getRootLine < 0)
        {
            return 0;
        }

        var afterRootSetup = getRootLine + 1;
        if (TryFindFirstAstoreLocalSlotAfter(lines, getRootLine, 1, 10, out var astoreLine, out _))
        {
            afterRootSetup = astoreLine + 1;
        }

        var firstCreate = FindFirstCubeListBuilderCreateLine(lines, afterRootSetup);
        if (firstCreate < 0)
        {
            return afterRootSetup;
        }

        if (JavapBytecodeStreamAnalyzer.TryFindLdcStringBeforeLine(lines, firstCreate, out var firstPartName) &&
            string.Equals(firstPartName, "root", StringComparison.Ordinal))
        {
            return firstCreate;
        }

        return afterRootSetup;
    }

    private static int FindFirstCubeListBuilderCreateLine(IReadOnlyList<string> lines, int startIdx)
    {
        for (var i = startIdx; i < lines.Count; i++)
        {
            if (lines[i].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<int, int> MergeBoxIntLocalConstants(
        IReadOnlyDictionary<int, int> segmentLocals,
        IReadOnlyDictionary<int, int> meshWideLocals)
    {
        var merged = new Dictionary<int, int>(meshWideLocals);
        foreach (var (slot, value) in segmentLocals)
        {
            // createLegs islands seed slot 3 with the quadruped default (6); do not clobber a body height
            // propagated from createBodyMesh on another island (e.g. SheepModel passes 12).
            if (meshWideLocals.TryGetValue(slot, out var wide) && wide > 0 && value == 6 && wide != 6)
            {
                continue;
            }

            merged[slot] = value;
        }

        return merged;
    }

    private static Dictionary<int, double> MergeBoxFloatLocalConstants(
        IReadOnlyDictionary<int, double> meshWideLocals,
        IReadOnlyDictionary<int, double> segmentLocals)
    {
        var merged = new Dictionary<int, double>(meshWideLocals);
        foreach (var (slot, value) in segmentLocals)
        {
            merged[slot] = value;
        }

        return merged;
    }

    /// <summary>
    /// Propagates the first int argument of <c>createBodyMesh</c> from call sites (e.g. <c>bipush 6</c> in
    /// <c>createBodyLayer</c>) into int locals used by delegated <c>createLegs</c> helpers on other islands.
    /// </summary>
    private static void ApplyMeshFactoryBodySizeFromCallSites(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("createBodyMesh", StringComparison.Ordinal) ||
                !line.Contains("(I", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryExtractBackwardIntConstant(lines, i - 1, map, out var bodySize) && bodySize > 0)
            {
                map[0] = bodySize;
                map[3] = bodySize;
                continue;
            }

            // SheepModel and similar pass bipush height then iconst booleans before createBodyMesh(IZZ…).
            for (var j = i - 1; j >= Math.Max(0, i - 12); j--)
            {
                if (lines[j].Contains("bipush", StringComparison.Ordinal) &&
                    JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var wideIv) && wideIv > 0)
                {
                    map[0] = (int)Math.Round(wideIv);
                    map[3] = (int)Math.Round(wideIv);
                    break;
                }
            }
        }
    }

    private static Dictionary<int, int> BuildBoxIntLocalConstantsFromSimpleIstores(IReadOnlyList<string> lines)
    {
        var map = new Dictionary<int, int>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseIstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[i - 1], out var iv))
            {
                map[slot] = (int)Math.Round(iv);
            }
        }

        return map;
    }

    private static void ApplyQuadrupedLegScaleFromCallSites(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        ApplyVoidHelperLegScaleParamSlots(lines, map);
        int? bodyMeshScale = null;
        int? legsCallScale = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal))
            {
                continue;
            }

            var isCreateLegs = line.Contains("createLegs", StringComparison.Ordinal);
            var isCreateBodyMesh = line.Contains("createBodyMesh", StringComparison.Ordinal);
            // createLegs:(PartDefinition;ZZI…) — int height is not the first parameter, so "(I" does not appear in javap comments.
            if (!isCreateLegs && !isCreateBodyMesh && !line.Contains("(I", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("LayerDefinition.create", StringComparison.Ordinal))
            {
                continue;
            }

            if (isCreateBodyMesh &&
                TryExtractBackwardIntConstant(lines, i - 1, map, out var sc) && sc > 0)
            {
                bodyMeshScale = sc;
            }

            if (isCreateLegs &&
                TryExtractBackwardIntConstant(lines, i - 1, map, out var legScale) && legScale > 0)
            {
                legsCallScale = legScale;
            }
        }

        // createLegs int param is the leg-box height source inside void helpers; prefer it over createBodyMesh
        // torso height when both appear in the same concat (order-independent).
        if (legsCallScale is > 0)
        {
            map[0] = legsCallScale.Value;
            map[3] = legsCallScale.Value;
        }
        else if (bodyMeshScale is > 0)
        {
            map[0] = bodyMeshScale.Value;
            map[3] = bodyMeshScale.Value;
        }
        else if ((lines.Any(static l => l.Contains("createLegs", StringComparison.Ordinal)) ||
                  lines.Any(static l => l.Contains("right_hind_leg", StringComparison.Ordinal) ||
                                        l.Contains("left_hind_leg", StringComparison.Ordinal))) &&
                 (!map.TryGetValue(3, out var existing) || existing <= 0))
        {
            // Adult quadruped body height when call-site bipush is on another island (e.g. createBodyLayer only),
            // or inside a void createLegs helper island with no upstream createBodyMesh call in the concat.
            map[3] = 6;
            if (!map.TryGetValue(0, out var slot0) || slot0 <= 0)
            {
                map[0] = 6;
            }
        }
    }

    /// <summary>
    /// Void helpers like <c>createLegs(PartDefinition,ZZI,CubeDeformation)</c> use <c>iload_3</c> for body height in
    /// <c>addBox</c> stacks; seed that slot when the island has no upstream <c>createBodyMesh</c> call in the same concat.
    /// </summary>
    private static void ApplyVoidHelperLegScaleParamSlots(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        if (!lines.Any(static l => l.Contains("createLegs", StringComparison.Ordinal)))
        {
            return;
        }

        if (!map.TryGetValue(3, out var h) || h <= 0)
        {
            map[3] = 6;
        }

        if (!map.TryGetValue(0, out var slot0) || slot0 <= 0)
        {
            map[0] = map[3];
        }
    }

    private static bool TryExtractBackwardIntConstant(IReadOnlyList<string> lines, int startIdx,
        IReadOnlyDictionary<int, int> intLocals, out int value)
    {
        value = 0;
        for (var j = startIdx; j >= 0; j--)
        {
            if (lines[j].Contains("invokestatic", StringComparison.Ordinal) ||
                lines[j].Contains("invokevirtual", StringComparison.Ordinal) ||
                lines[j].Contains("invokespecial", StringComparison.Ordinal))
            {
                break;
            }

            if (JavapBytecodeStreamAnalyzer.IsBackwardStackNoiseLine(lines[j]) ||
                lines[j].Contains("iconst_", StringComparison.Ordinal))
            {
                continue;
            }

            if (lines[j].Contains("bipush", StringComparison.Ordinal) ||
                lines[j].Contains("sipush", StringComparison.Ordinal))
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var wideIv))
                {
                    value = (int)Math.Round(wideIv);
                    return true;
                }
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var iv))
            {
                value = (int)Math.Round(iv);
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(lines[j], out var slot) && intLocals.TryGetValue(slot, out var localIv))
            {
                value = localIv;
                return true;
            }

            break;
        }

        return false;
    }

    /// <summary>Named or obfuscated <c>getRoot</c> returning a part tree root.</summary>
    private static bool IsMeshRootGetRootInvokeLine(string line)
    {
        if (!line.Contains("getRoot:", StringComparison.Ordinal))
        {
            return false;
        }

        // Named jars: MeshDefinition / PartDefinition appear in the javap comment. Avoid a bare "invokevirtual" match —
        // it can correlate with unrelated callsites in the same method and corrupt local-slot tracking.
        return line.Contains("MeshDefinition", StringComparison.Ordinal) ||
               line.Contains("PartDefinition", StringComparison.Ordinal) ||
               line.Contains("/PartDefinition;", StringComparison.Ordinal);
    }

    /// <summary>Skips blank lines and <c>pop</c> between a key instruction and the first <c>astore</c>.</summary>
    private static bool TryFindFirstAstoreLocalSlotAfter(IReadOnlyList<string> lines, int startIdx, int minOffset,
        int maxOffset, out int astoreLineIdx, out int slot)
    {
        astoreLineIdx = -1;
        slot = 0;
        for (var d = minOffset; d <= maxOffset && startIdx + d < lines.Count; d++)
        {
            var ln = lines[startIdx + d];
            if (string.IsNullOrWhiteSpace(ln))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.IsJavapMethodCodeHeaderLine(ln))
            {
                return false;
            }

            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(ln))
            {
                return false;
            }

            if (JavapBytecodeStreamAnalyzer.IsJavapPopLine(ln))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(ln, out slot))
            {
                astoreLineIdx = startIdx + d;
                return true;
            }
        }

        return false;
    }

    private static string? FindLdcPartNameImmediatelyBeforeBinding(List<string> lines, int bindingIdx)
    {
        if (bindingIdx < 0 || bindingIdx >= lines.Count ||
            !JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[bindingIdx]))
        {
            return null;
        }

        return TryResolvePartNameForBinding(lines, bindingIdx, new Dictionary<int, int>(), out var name) ? name : null;
    }

    private static int? TryInferReceiverLocalSlotForBinding(List<string> lines, int bindingIdx, string partName)
    {
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 16; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(lines[j]);
            if (!sm.Success || !string.Equals(sm.Groups[1].Value, partName, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryFindPartDefinitionReceiverAloadLineBeforeIndex(lines, j, bindingIdx, out var recvLine) &&
                JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[recvLine], out var loc))
            {
                return loc;
            }

            return null;
        }

        if (TryFindPartDefinitionReceiverAloadLineBeforeIndex(lines, bindingIdx, bindingIdx, out var fallbackLine) &&
            JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[fallbackLine], out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private static bool TryFindPartDefinitionReceiverAloadLineBeforeIndex(List<string> lines, int beforeIdx,
        int bindingIdx, out int aloadLineIdx)
    {
        aloadLineIdx = 0;
        var start = Math.Max(0, beforeIdx - 40);
        for (var j = beforeIdx - 1; j >= start; j--)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[j], out _))
            {
                continue;
            }

            if (!IsPartDefinitionReceiverAloadBeforeBinding(lines, j, bindingIdx))
            {
                continue;
            }

            aloadLineIdx = j;
            return true;
        }

        return false;
    }

    /// <summary>Last <c>aload</c> local in <c>[beforeIdx-maxLookback, beforeIdx)</c> — the PartDefinition receiver for addOrReplaceChild.</summary>
    private static bool TryFindReceiverAloadBeforeIndex(List<string> lines, int beforeIdx, int maxLookback,
        out int slot)
    {
        slot = 0;
        int? last = null;
        var start = Math.Max(0, beforeIdx - maxLookback);
        for (var j = beforeIdx - 1; j >= start; j--)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[j]))
            {
                break;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[j], out var loc))
            {
                last = loc;
            }
        }

        if (last is int l)
        {
            slot = l;
            return true;
        }

        return false;
    }

    private static void AttachLiftedPartToForest(JsonArray rootForest,
        List<string> lines, int bindingLineIdx, JsonObject partNode,
        string partName, List<PendingPartAttach> pendingAttaches)
    {
        var graphAtBind = BuildReceiverLocalSlotGraph(lines.GetRange(0, Math.Min(bindingLineIdx + 1, lines.Count)));
        var recv = TryInferReceiverLocalSlotForBinding(lines, bindingLineIdx, partName);
        var parentPartId = recv is { } r
            ? ResolveParentPartIdFromReceiverSlot(r, graphAtBind)
            : null;

        var keepFlatRootLeg = IsTinyBabyQuadrupedLeg(partName, partNode) ||
            IsGenericQuadrupedHelperLegContext(lines, partName);
        if (keepFlatRootLeg && IsStandardQuadrupedLegPartName(partName))
        {
            parentPartId = null;
        }

        var standardLegParent = ShouldAttachStandardQuadrupedLegUnderBody(lines, partName, partNode)
            ? "body"
            : null;
        if (string.IsNullOrEmpty(parentPartId) &&
            !keepFlatRootLeg &&
            GeometryLiftForestMerge.KnownNestedChildToParent.TryGetValue(partName, out var knownParent) &&
            GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, knownParent, out _))
        {
            parentPartId = knownParent;
        }
        else if (string.IsNullOrEmpty(parentPartId) &&
                 standardLegParent is not null &&
                 GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, standardLegParent, out _))
        {
            parentPartId = standardLegParent;
        }
        else if (string.IsNullOrEmpty(parentPartId) &&
                 !keepFlatRootLeg &&
                 standardLegParent is not null &&
                 HasFuturePartBinding(lines, bindingLineIdx, standardLegParent))
        {
            pendingAttaches.Add(new PendingPartAttach(standardLegParent, partNode, partName));
            return;
        }

        if (string.IsNullOrEmpty(parentPartId))
        {
            rootForest.Add(partNode);
            return;
        }

        if (!GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, parentPartId, out var parentObj) ||
            parentObj is null)
        {
            pendingAttaches.Add(new PendingPartAttach(parentPartId, partNode, partName));
            return;
        }

        GeometryLiftForestMerge.AttachPartUnderParent(parentObj, partNode, partName);
    }

    private static bool IsStandardQuadrupedLegPartName(string partName) =>
        partName is "right_hind_leg" or "left_hind_leg" or "right_front_leg" or "left_front_leg";

    private static bool HasFuturePartBinding(List<string> lines, int bindingLineIdx, string partName)
    {
        for (var i = bindingLineIdx + 1; i < lines.Count; i++)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                continue;
            }

            var head = lines.GetRange(0, i + 1);
            var child = FindPartNameNearAddOrReplaceChild(head, new Dictionary<int, int>()) ??
                        FindLdcPartNameImmediatelyBeforeBinding(lines, i);
            if (string.Equals(child, partName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGenericQuadrupedHelperLegContext(List<string> lines, string partName) =>
        IsStandardQuadrupedLegPartName(partName) &&
        lines.Any(static l =>
            l.Contains("QuadrupedModel.createBodyMesh", StringComparison.Ordinal) ||
            l.Contains("QuadrupedModel.createLegs", StringComparison.Ordinal));

    private static bool ShouldAttachStandardQuadrupedLegUnderBody(List<string> lines, string partName,
        JsonObject partNode) =>
        IsStandardQuadrupedLegPartName(partName) &&
        !IsTinyBabyQuadrupedLeg(partName, partNode) &&
        !lines.Any(static l => l.Contains("QuadrupedModel.createLegs", StringComparison.Ordinal));

    private static bool IsTinyBabyQuadrupedLeg(string partName, JsonObject partNode)
    {
        if (!IsStandardQuadrupedLegPartName(partName) ||
            partNode["cuboids"] is not JsonArray { Count: > 0 } cuboids ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 2 ||
            to.Count < 2)
        {
            return false;
        }

        var height = to[1]?.GetValue<double>() - from[1]?.GetValue<double>();
        return height is >= 0 and <= 3.0;
    }

    /// <summary>
    /// After <c>addOrReplaceChild</c>, the receiver local is re-mapped to the child part id; walk
    /// <see cref="ReceiverSlotEntry.ParentReceiverSlot"/> to find the parent mesh part id.
    /// </summary>
    private static string? ResolveParentPartIdFromReceiverSlot(int receiverSlot,
        Dictionary<int, ReceiverSlotEntry> receiverSlotGraph)
    {
        if (!receiverSlotGraph.TryGetValue(receiverSlot, out var entry))
        {
            return null;
        }

        if (entry.ParentReceiverSlot is { } parentSlot &&
            receiverSlotGraph.TryGetValue(parentSlot, out var parentEntry) &&
            !string.IsNullOrEmpty(parentEntry.PartId))
        {
            return parentEntry.PartId;
        }

        return entry.PartId;
    }

    private static void FlushPendingPartAttaches(JsonArray rootForest, List<PendingPartAttach> pending,
        bool hoistOrphansToRoot = false)
    {
        var progress = true;
        while (progress && pending.Count > 0)
        {
            progress = false;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (!GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, p.ParentPartId, out var parentObj) ||
                    parentObj is null)
                {
                    continue;
                }

                GeometryLiftForestMerge.AttachPartUnderParent(parentObj, p.PartNode, p.PartName);
                pending.RemoveAt(i);
                progress = true;
            }
        }

        if (!hoistOrphansToRoot)
        {
            return;
        }

        foreach (var p in pending)
        {
            rootForest.Add(p.PartNode);
        }

        pending.Clear();
    }
}
