using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static AutoPBR.Tools.GeometryCompiler.GeometryLiftCoordinateRounding;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Lifts a flat part tree from Mojang 26.x-style <c>javap -c</c> for static mesh factories (<c>MeshDefinition</c> and
/// <c>LayerDefinition</c> static methods once concatenated): <c>CubeListBuilder.texOffs</c>,
/// <c>addBox(FFFFFF)</c> / extended <c>addBox(FFFFFFL…CubeDeformation;FF)</c> / <c>addBox(String,FFFFFF)</c>,
/// texCrop-style <c>addBox(String,FFFIII…)</c>, direction-mask overload via <c>java/util/Set</c> (full box approximation),
/// and <c>PartPose</c> before <c>PartDefinition.addOrReplaceChild</c>.
/// </summary>
internal static partial class JavapFloatGeometryMeshLift
{
    [ThreadStatic]
    private static MojangMappingsParser? _maps;

    [ThreadStatic]
    private static int _delegationDepth;
    private static int _invokeStaticPartNameBindingOrdinal;

    /// <summary>
    /// Max bytecode lines to scan backward from <c>addOrReplaceChild</c> for a trailing <c>ldc</c> part id
    /// (PartPose / float / int stack operands only — not across <c>addBox</c> or builder calls).
    /// Calibrated against the full geometry-compiler test suite (10 lines matches 24; 9 fails).
    /// </summary>
    private const int PartNameLdcImmediateLookbackLines = 10;

    public static bool TryLift(string meshFactoryJavap, out JsonArray roots, out List<string> notes,
        MojangMappingsParser? maps = null, int delegationDepth = 0,
        IReadOnlyDictionary<string, int[][]>? staticIntMatrices = null,
        IReadOnlyDictionary<string, float[]>? staticFloatArrays = null)
    {
        roots = new JsonArray();
        notes = new List<string>();
        try
        {
            _delegationDepth = delegationDepth;
            _maps = maps;
            _staticIntMatrices = staticIntMatrices;
            _staticFloatArrays = staticFloatArrays;
            if (string.IsNullOrWhiteSpace(meshFactoryJavap) ||
                !JavapMeshBytecodeProfiles.ContainsMeshSignals(meshFactoryJavap))
            {
                return false;
            }

            meshFactoryJavap = meshFactoryJavap.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            meshFactoryJavap = BytecodeMeshResolution.NormalizeMeshIslandBoundaries(meshFactoryJavap);

            JsonArray rootChildren;
        if (meshFactoryJavap.Contains(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker,
                StringComparison.Ordinal))
        {
            var islands = meshFactoryJavap
                .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            var meshWideLines = JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(
                meshFactoryJavap.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
            rootChildren = new JsonArray();
            foreach (var t in islands)
            {
                if (t.Length == 0 || !JavapMeshBytecodeProfiles.ContainsMeshSignals(t))
                {
                    continue;
                }

                var useMeshWideScope = JavapLiftPartTreeBuilder.IslandUsesMeshWideConstantScope(t);
                var islandMeshWide = useMeshWideScope ? meshWideLines : null;
                var prevMatrices = _staticIntMatrices;
                var prevFloatArrays = _staticFloatArrays;
                if (!useMeshWideScope)
                {
                    _staticIntMatrices = null;
                    _staticFloatArrays = null;
                }

                try
                {
                    if (!JavapLiftPartTreeBuilder.TryCollectLiftedRootChildren(t, notes, out var islandKids, islandMeshWide) ||
                        islandKids.Count == 0)
                    {
                        continue;
                    }

                    foreach (var n in islandKids)
                    {
                        GeometryLiftJsonMerge.MergeRootChildLastWinsByPartId(rootChildren, n);
                    }
                }
                finally
                {
                    _staticIntMatrices = prevMatrices;
                    _staticFloatArrays = prevFloatArrays;
                }
            }

            GeometryLiftForestMerge.ApplyMultiIslandPostMerge(rootChildren);
        }
        else
        {
            if (!JavapLiftPartTreeBuilder.TryCollectLiftedRootChildren(meshFactoryJavap, notes, out rootChildren))
            {
                return false;
            }
        }

        if (rootChildren.Count == 0)
        {
            return false;
        }

            roots = GeometryLiftOutputAssembly.WrapSyntheticRoot(rootChildren);
            return true;
        }
        finally
        {
            _staticIntMatrices = null;
            _staticFloatArrays = null;
            _maps = null;
            _delegationDepth = 0;
        }
    }

    /// <summary>
    /// <c>javap -c</c> wraps long comments so <c>PartDefinition.addOrReplaceChild</c> can be split across two physical lines.
    /// Fold those continuations into the previous line so mesh binding / pose / addBox detection sees whole tokens.
    /// </summary>
    internal static List<string> FoldJavapWrappedBytecodeLinesForTests(List<string> lines) =>
        JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(lines);

    internal static string MergeJavapCommentContinuationForTests(List<string> seg, int invokeLineIdx) =>
        JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, invokeLineIdx);


    private static bool TryLiftSegment(List<string> seg,
        IReadOnlyDictionary<int, double> cubeDeformationInflateByRefSlot,
        IReadOnlyDictionary<int, double> poseFloatLocals,
        IReadOnlyDictionary<int, double> boxFloatLocals,
        IReadOnlyDictionary<int, int> boxIntLocals,
        IReadOnlyList<string> meshWideLines,
        out JsonObject partNode,
        List<string> notes)
    {
        partNode = new JsonObject();
        // javac usually pushes the child name immediately before addOrReplaceChild; some factories only ldc the
        // name after CubeListBuilder.create (no String ldc before create). Prefer the binding-site name first.
        var partName = (TryResolvePartNameForBinding(seg, seg.Count - 1, boxIntLocals, out var boundName)
                           ? boundName
                           : null) ??
                       FindPartNameNearAddOrReplaceChild(seg, boxIntLocals) ??
                       FindFirstPartName(seg);
        if (string.IsNullOrEmpty(partName))
        {
            notes.Add(
                "Mesh segment skipped: could not resolve part id (no ldc String before addOrReplaceChild and none before CubeListBuilder.create).");
            return false;
        }

        partNode["id"] = partName;
        var addBoxIndices = new List<int>();
        for (var i = 0; i < seg.Count; i++)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedFloatAddBoxLine(seg[i], out _))
            {
                addBoxIndices.Add(i);
            }
        }

        _ = TryResolvePoseClassHint(seg, out var poseClassHint);

        if (addBoxIndices.Count == 0)
        {
            var poseOnlyWarnings = new List<string>();
            if (!TryParsePartPoseImmediatelyBeforeBinding(seg, poseClassHint, poseFloatLocals, boxIntLocals,
                    poseOnlyWarnings, out var poseOnly) &&
                !TryParsePose(seg, 0, seg.Count - 1, poseClassHint, poseFloatLocals, boxIntLocals, poseOnlyWarnings,
                    out poseOnly))
            {
                poseOnly = ZeroPose();
            }
            else if (poseOnlyWarnings.Count > 0)
            {
                AppendPoseWarnings(poseOnly, poseOnlyWarnings);
            }

            StampSetupAnimPivot(poseOnly);
            partNode["pose"] = poseOnly;
            partNode["cuboids"] = new JsonArray();
            partNode["children"] = new JsonArray();
            return true;
        }

        FilterAddBoxIndicesForReusedBuilderTemplate(seg, addBoxIndices);

        if (addBoxIndices.Count == 0)
        {
            var bindingIdx = seg.Count - 1;
            for (var i = seg.Count - 1; i >= 0; i--)
            {
                if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
                {
                    bindingIdx = i;
                    break;
                }
            }

            if (TryFindReusedBuilderAloadBeforeBinding(seg, bindingIdx, out _, out _))
            {
                var poseOnlyWarnings = new List<string>();
                if (!TryParsePartPoseImmediatelyBeforeBinding(seg, poseClassHint, poseFloatLocals, boxIntLocals,
                        poseOnlyWarnings, out var poseOnly) &&
                    !TryParsePose(seg, 0, bindingIdx - 1, poseClassHint, poseFloatLocals, boxIntLocals,
                        poseOnlyWarnings, out poseOnly))
                {
                    poseOnly = ZeroPose();
                }
                else if (poseOnlyWarnings.Count > 0)
                {
                    AppendPoseWarnings(poseOnly, poseOnlyWarnings);
                }

                StampSetupAnimPivot(poseOnly);
                partNode["pose"] = poseOnly;
                partNode["cuboids"] = new JsonArray();
                partNode["children"] = new JsonArray();
                return true;
            }

            notes.Add($"segment '{partName}': addBox indices empty after reused-builder filter.");
            return false;
        }

        var provenance = meshFactoryJavapContainsNamedMojangApi(seg)
            ? "javap lift CubeListBuilder.addBox profile_named_26_1_2_float"
            : "javap lift fluent addBox profile_proguard_obf";

        JsonObject poseJson;
        var lastBox = addBoxIndices[^1];
        var poseEnd = seg.Count - 1;
        if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[^1]))
        {
            poseEnd = seg.Count - 2;
        }

        for (var k = lastBox + 1; k < seg.Count; k++)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[k]))
            {
                poseEnd = k - 1;
                break;
            }
        }

        var poseWarnings = new List<string>();
        if (!TryParsePartPoseImmediatelyBeforeBinding(seg, poseClassHint, poseFloatLocals, boxIntLocals, poseWarnings,
                out poseJson) &&
            !TryParsePose(seg, lastBox + 1, poseEnd, poseClassHint, poseFloatLocals, boxIntLocals, poseWarnings,
                out poseJson))
        {
            poseJson = ZeroPose();
        }
        else if (poseWarnings.Count > 0)
        {
            AppendPoseWarnings(poseJson, poseWarnings);
        }

        StampSetupAnimPivot(poseJson);
        partNode["pose"] = poseJson;
        var cuboids = new JsonArray();

        for (var bi = 0; bi < addBoxIndices.Count; bi++)
        {
            var lineIdx = addBoxIndices[bi];
            var floatMinIdx = bi > 0 ? addBoxIndices[bi - 1] + 1 : 0;
            var addBoxInvokeDescriptor = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, lineIdx);
            var shape = JavapMeshBytecodeProfiles.ClassifyAddBoxInvokeShape(addBoxInvokeDescriptor);

            double ox;
            double oy;
            double oz;
            double sx;
            double sy;
            double sz;
            int scanFrom;
            string? mirrorQuadKey = null;
            int? uvSpanW = null;
            int? uvSpanH = null;
            var usesEmbeddedTexCropUv = false;

            switch (shape)
            {
                case AddBoxInvokeShape.StringQuadThreeFloatThreeIntsCubeDefTexCropInts:
                case AddBoxInvokeShape.StringQuadThreeFloatThreeIntsTexCropIntsNoDef:
                    if (!TryParseStringQuadTexCropBoxBackward(seg, lineIdx - 1,
                            shape == AddBoxInvokeShape.StringQuadThreeFloatThreeIntsCubeDefTexCropInts,
                            out ox, out oy, out oz, out var dx, out var dy, out var dz, out var tu, out var tv,
                            out mirrorQuadKey, out scanFrom))
                    {
                        notes.Add($"addBox at line {lineIdx}: could not parse string-quad texCrop stack.");
                        continue;
                    }

                    sx = dx;
                    sy = dy;
                    sz = dz;
                    uvSpanW = tu;
                    uvSpanH = tv;
                    usesEmbeddedTexCropUv = true;
                    break;

                default:
                    if (addBoxInvokeDescriptor.Contains("Ljava/lang/String;FFFIII", StringComparison.Ordinal))
                    {
                        if (!TryParseStringQuadTexCropBoxBackward(seg, lineIdx - 1,
                                addBoxInvokeDescriptor.Contains("CubeDeformation", StringComparison.Ordinal),
                                out ox, out oy, out oz, out var tdx, out var tdy, out var tdz, out var tcropU, out var tcropV,
                                out mirrorQuadKey, out scanFrom))
                        {
                            notes.Add($"addBox at line {lineIdx}: could not parse string-quad texCrop stack.");
                            continue;
                        }

                        sx = tdx;
                        sy = tdy;
                        sz = tdz;
                        uvSpanW = tcropU;
                        uvSpanH = tcropV;
                        usesEmbeddedTexCropUv = true;
                        break;
                    }

                    var stringOverload = shape == AddBoxInvokeShape.Float6StringQuadKey ||
                        shape == AddBoxInvokeShape.Float6StringQuadKeyDirectionFaceSet ||
                        (shape == AddBoxInvokeShape.Unknown &&
                         addBoxInvokeDescriptor.Contains("Ljava/lang/String;FFFFFF",
                             StringComparison.Ordinal));
                    if (!TryParseSixFloatsBackward(seg, lineIdx - 1, floatMinIdx, addBoxInvokeDescriptor, boxFloatLocals,
                            boxIntLocals, out ox, out oy, out oz, out sx, out sy, out sz, out scanFrom, stringOverload,
                            out mirrorQuadKey))
                    {
                        notes.Add($"addBox at line {lineIdx}: could not parse six floats.");
                        continue;
                    }

                    break;
            }

            double u;
            double v;
            if (usesEmbeddedTexCropUv)
            {
                u = uvSpanW ?? 0;
                v = uvSpanH ?? 0;
            }
            else if (!TryParseTexOffsFromStaticMatrixBackward(seg, scanFrom,
                         bi > 0 ? addBoxIndices[bi - 1] + 1 : 0, boxIntLocals, out u, out v, out scanFrom) &&
                     !TryParseTexOffsBackward(seg, scanFrom,
                         floatMinIdx: bi > 0 ? addBoxIndices[bi - 1] + 1 : 0, out u, out v, out scanFrom) &&
                     !TryParseTexOffsImmediatelyBeforeAddBox(seg, lineIdx, out u, out v))
            {
                notes.Add($"addBox at line {lineIdx}: could not parse texOffs (defaulting to 0,0).");
                u = 0;
                v = 0;
            }

            var prevAddBoxLine = bi > 0 ? addBoxIndices[bi - 1] : -1;
            var mirrorU = ResolveMirrorUForCuboidFluentRegion(seg, prevAddBoxLine + 1, lineIdx - 1);

            var from = new JsonArray { Round(ox), Round(oy), Round(oz) };
            var to = new JsonArray { Round(ox + sx), Round(oy + sy), Round(oz + sz) };
            var uv = new JsonArray { (int)Math.Round(u), (int)Math.Round(v) };

            var maskResult = DirectionMaskParseResult.NotDirectionMask;
            List<string>? faceMaskList = null;
            if (shape == AddBoxInvokeShape.Float6DirectionFaceSet ||
                shape == AddBoxInvokeShape.Float6StringQuadKeyDirectionFaceSet ||
                (shape == AddBoxInvokeShape.Unknown &&
                 addBoxInvokeDescriptor.Contains("Ljava/util/Set", StringComparison.Ordinal)))
            {
                maskResult = TryParseDirectionFaceMaskForAddBox(seg, lineIdx, prevAddBoxLine, meshWideLines, _maps,
                    out faceMaskList);
            }

            var liftKind = ResolveLiftKindForAddBox(shape, addBoxInvokeDescriptor, maskResult);
            var provenanceCube = provenance;
            if (liftKind == LiftDirectionMaskFullBox)
            {
                provenanceCube += " direction_masked_faces_full_box_approx";
            }

            var textureKey = string.IsNullOrEmpty(mirrorQuadKey)
                ? "#skin"
                : mirrorQuadKey.StartsWith('#')
                    ? mirrorQuadKey
                    : "#" + mirrorQuadKey;

            var inferMinLine = prevAddBoxLine >= 0 ? prevAddBoxLine + 1 : 0;
            var obfDef = false;
            double? inflateAmt = null;
            if (TryResolveUniformCubeDeformationInflate(seg, lineIdx, inferMinLine, cubeDeformationInflateByRefSlot,
                    _maps, out var inflateResolved) &&
                Math.Abs(inflateResolved) > 1e-12)
            {
                inflateAmt = inflateResolved;
                obfDef = !JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, lineIdx).Contains("CubeDeformation", StringComparison.Ordinal);
            }

            var warnings = BuildCuboidLiftWarnings(liftKind, maskResult, obfDef);
            var c = CreateCuboidJson(from, to, uv, textureKey, provenanceCube, liftKind, warnings,
                faceMaskList, mirrorU, uvSpanW, uvSpanH, inflateAmt);

            cuboids.Add(c);
        }

        if (cuboids.Count == 0)
        {
            notes.Add($"segment '{partName}': addBox present but no cuboids parsed ({addBoxIndices.Count} addBox site(s)).");
            return false;
        }

        partNode["cuboids"] = cuboids;
        partNode["children"] = new JsonArray();
        return true;
    }

    private static bool meshFactoryJavapContainsNamedMojangApi(List<string> seg) =>
        seg.Exists(l => l.Contains("CubeListBuilder", StringComparison.Ordinal));

    private static bool TryResolvePoseClassHint(List<string> seg, out string? poseClassHint)
    {
        poseClassHint = null;
        for (var i = seg.Count - 1; i >= 0; i--)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                continue;
            }

            if (!JavapMeshBytecodeProfiles.TryInferBindingTypesFromLine(seg[i], out _, out var poseShort))
            {
                continue;
            }

            poseClassHint = poseShort.Contains('.', StringComparison.Ordinal) ||
                             poseShort.Contains('/', StringComparison.Ordinal)
                ? "PartPose"
                : poseShort;
            return true;
        }

        return false;
    }

    internal static string? TryResolveBindingPartNameForDiagnostics(List<string> lines, int bindingIdx) =>
        TryResolvePartNameForBinding(lines, bindingIdx, new Dictionary<int, int>(), out var name) ? name : null;

    /// <summary>
    /// Resolves the child id for <c>PartDefinition.addOrReplaceChild</c> at <paramref name="bindingIdx"/>.
    /// </summary>
    private static string? FindPartNameNearAddOrReplaceChild(List<string> seg,
        IReadOnlyDictionary<int, int> boxIntLocals)
    {
        for (var i = seg.Count - 1; i >= 0; i--)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                continue;
            }

            if (TryResolvePartNameForBinding(seg, i, boxIntLocals, out var name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Mojang reuses mirrored leg builders (<c>aload_2</c> / <c>aload_3</c>) without <c>addBox</c> in the binding slice;
    /// extend the slice backward to the matching <c>astore</c> template block.
    /// </summary>
    private static bool TryExpandSliceForReusedCubeListBuilder(List<string> lines, int bindingLineIdx, ref int sliceStart)
    {
        if (!TryFindAloadLocalSlotBeforeBinding(lines, bindingLineIdx, out var builderSlot))
        {
            return false;
        }

        if (!TryFindLastAstoreForLocalSlot(lines, bindingLineIdx, builderSlot, out var astoreLine))
        {
            return false;
        }

        var templateStart = astoreLine;
        for (var j = astoreLine - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[j], out _))
            {
                templateStart = j + 1;
                break;
            }

            if (lines[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(lines[j]))
            {
                templateStart = j;
                break;
            }
        }

        if (templateStart >= sliceStart)
        {
            return false;
        }

        sliceStart = templateStart;
        return true;
    }

    private static bool TryFindAloadLocalSlotBeforeBinding(List<string> lines, int bindingLineIdx, out int slot)
    {
        slot = 0;
        for (var j = bindingLineIdx - 1; j >= 0 && j > bindingLineIdx - 16; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[j], out slot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects quadruped leg-style reuse: <c>ldc</c> part name, <c>aload</c> stored fluent builder, no
    /// <c>CubeListBuilder.create</c> between that <c>aload</c> and <c>addOrReplaceChild</c>.
    /// Skips <c>aload</c> of <c>CubeDeformation</c> operands immediately before masked <c>addBox</c> calls.
    /// </summary>
    private static bool TryFindReusedBuilderAloadBeforeBinding(List<string> seg, int bindingIdx, out int builderSlot,
        out int astoreLine)
    {
        builderSlot = 0;
        astoreLine = 0;
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 16; j--)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out builderSlot))
            {
                continue;
            }

            if (IsPartDefinitionReceiverAloadBeforeBinding(seg, j, bindingIdx))
            {
                continue;
            }

            if (SegmentHasCubeListBuilderCreateAfterAload(seg, builderSlot, bindingIdx))
            {
                continue;
            }

            var hasLdcNameBeforeAload = false;
            for (var k = j - 1; k >= 0 && k > j - 5; k--)
            {
                if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
                {
                    hasLdcNameBeforeAload = true;
                    break;
                }
            }

            if (!hasLdcNameBeforeAload)
            {
                continue;
            }

            if (!TryFindLastAstoreForLocalSlot(seg, bindingIdx, builderSlot, out astoreLine))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// True for <c>aload_0</c>-style mesh receivers (<c>aload</c> then immediate <c>ldc</c> part name), not fluent builders.
    /// </summary>
    private static bool IsQuadrupedLegPartName(string partName) =>
        partName.Contains("leg", StringComparison.Ordinal);

    private static bool IsPartDefinitionReceiverAloadBeforeBinding(List<string> seg, int aloadLineIdx, int bindingIdx)
    {
        for (var k = aloadLineIdx + 1; k < bindingIdx && k < aloadLineIdx + 4; k++)
        {
            if (JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, k).Contains("PartPose", StringComparison.Ordinal))
            {
                return false;
            }

            if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
            {
                return true;
            }
        }

        return false;
    }

    private static void FilterAddBoxIndicesForReusedBuilderTemplate(List<string> seg, List<int> addBoxIndices)
    {
        var bindingIdx = seg.Count - 1;
        if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[bindingIdx]))
        {
            for (var i = seg.Count - 1; i >= 0; i--)
            {
                if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
                {
                    bindingIdx = i;
                    break;
                }
            }
        }

        if (!TryFindReusedBuilderAloadBeforeBinding(seg, bindingIdx, out var builderSlot, out _))
        {
            return;
        }

        if (addBoxIndices.Count <= 1)
        {
            return;
        }

        var templateAstoreSlots = new HashSet<int>();
        foreach (var boxLine in addBoxIndices)
        {
            if (TryGetAstoreSlotAfterAddBox(seg, boxLine, out var astoreSlot))
            {
                templateAstoreSlots.Add(astoreSlot);
            }
        }

        if (templateAstoreSlots.Count <= 1)
        {
            return;
        }

        addBoxIndices.RemoveAll(i => !AddBoxBelongsToBuilderLocalSlot(seg, i, builderSlot));
    }

    private static bool TryGetAstoreSlotAfterAddBox(List<string> seg, int addBoxLine, out int astoreSlot)
    {
        astoreSlot = 0;
        for (var j = addBoxLine + 1; j < seg.Count && j <= addBoxLine + 32; j++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(seg[j], out astoreSlot))
            {
                return true;
            }

            if (seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Cow/quadruped legs store mirrored templates in <c>astore_2</c>/<c>astore_3</c>; a binding slice can contain both templates.
    /// </summary>
    private static bool AddBoxBelongsToBuilderLocalSlot(List<string> seg, int addBoxLine, int builderSlot) =>
        TryGetAstoreSlotAfterAddBox(seg, addBoxLine, out var astoreSlot) && astoreSlot == builderSlot;

    private static bool TryFindLastAstoreForLocalSlot(List<string> lines, int beforeLineIdx, int slot, out int astoreLine)
    {
        astoreLine = 0;
        for (var j = beforeLineIdx - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[j], out var s) && s == slot)
            {
                astoreLine = j;
                return true;
            }
        }

        return false;
    }

    private static bool SegmentHasCubeListBuilderCreateAfterAload(List<string> seg, int aloadSlot, int bindingIdx)
    {
        var aloadLine = -1;
        for (var j = bindingIdx - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out var s) && s == aloadSlot)
            {
                aloadLine = j;
                break;
            }
        }

        if (aloadLine < 0)
        {
            return false;
        }

        for (var j = aloadLine + 1; j < bindingIdx; j++)
        {
            if (seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInferLoopPartNameFromSegment(List<string> seg, int bindingIdx, int loopSlot, int iteration,
        out string? name)
    {
        name = null;
        for (var j = bindingIdx - 1; j >= 0; j--)
        {
            var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("(I)Ljava/lang/String;", StringComparison.Ordinal))
            {
                continue;
            }

            var m = Regex.Match(line, @"\.(\w+):\(I\)Ljava/lang/String;", RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                continue;
            }

            for (var k = j - 1; k >= 0 && k > j - 16; k--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(seg[k], out var slot) && slot == loopSlot)
                {
                    name = InferIndexedPartNameFromFactoryMethod(m.Groups[1].Value, iteration, seg);
                    return !string.IsNullOrEmpty(name);
                }
            }
        }

        return false;
    }

    private static bool TryParseInvokeStaticIndexedPartNameBeforeBinding(List<string> seg, int bindingIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out string? name)
    {
        name = null;
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 64; j--)
        {
            var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("Ljava/lang/String;", StringComparison.Ordinal) ||
                !Regex.IsMatch(line, @"\.(\w+):\(I\)Ljava/lang/String;", RegexOptions.CultureInvariant))
            {
                continue;
            }

            var method = Regex.Match(line, @"\.(\w+):\(I\)Ljava/lang/String;", RegexOptions.CultureInvariant).Groups[1]
                .Value;
            if (TryParseIntOperandImmediatelyBefore(seg, j, boxIntLocals, out var index))
            {
                name = InferIndexedPartNameFromFactoryMethod(method, index, seg);
                return !string.IsNullOrEmpty(name);
            }

            if (method.Contains("Name", StringComparison.Ordinal))
            {
                index = _invokeStaticPartNameBindingOrdinal++;
                name = InferIndexedPartNameFromFactoryMethod(method, index, seg);
                return !string.IsNullOrEmpty(name);
            }
        }

        return false;
    }

    private static bool TryParseIntOperandImmediatelyBefore(List<string> seg, int invokeLineIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out int value)
    {
        value = 0;
        for (var j = invokeLineIdx - 1; j >= 0 && j > invokeLineIdx - 64; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out var iv))
            {
                value = (int)iv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(seg[j], out var slot))
            {
                if (boxIntLocals.TryGetValue(slot, out value))
                {
                    return true;
                }

                if (TryResolveIntLocal(seg, j, slot, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryResolveIntLocal(List<string> seg, int useLineIdx, int slot, out int value)
    {
        value = 0;
        for (var j = useLineIdx - 1; j >= 0 && j > useLineIdx - 40; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseIstoreLocalSlot(seg[j], out var stored) && stored == slot)
            {
                for (var k = j - 1; k >= 0 && k > j - 6; k--)
                {
                    if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[k], out var iv))
                    {
                        value = (int)iv;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string InferIndexedPartNameFromFactoryMethod(string methodName, int index, List<string>? seg = null)
    {
        if (string.Equals(methodName, "getSegmentName", StringComparison.Ordinal) &&
            seg is not null &&
            seg.Any(static l => l.Contains("MagmaCubeModel", StringComparison.Ordinal)))
        {
            return $"cube{index}";
        }

        if (methodName.Contains("Tentacle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(methodName, "tentacle", StringComparison.Ordinal))
        {
            return $"tentacle{index}";
        }

        if (methodName.Contains("Neck", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(methodName, "neckName", StringComparison.Ordinal))
        {
            return $"neck{index}";
        }

        if (string.Equals(methodName, "tailName", StringComparison.Ordinal))
        {
            return $"tail{index}";
        }

        if (string.Equals(methodName, "boxName", StringComparison.Ordinal))
        {
            return $"box{index}";
        }

        if (methodName.Contains("Cube", StringComparison.OrdinalIgnoreCase))
        {
            return $"cube{index}";
        }

        if (methodName.Contains("Segment", StringComparison.OrdinalIgnoreCase))
        {
            return $"segment{index}";
        }

        if (methodName.Contains("Layer", StringComparison.OrdinalIgnoreCase))
        {
            return $"layer{index}";
        }

        if (string.Equals(methodName, "getPartName", StringComparison.Ordinal))
        {
            return $"part{index}";
        }

        if (string.Equals(methodName, "createSpikeName", StringComparison.Ordinal))
        {
            return $"spike{index}";
        }

        return $"{methodName}_{index}";
    }

    private static bool TryResolvePartNameForBinding(List<string> seg, int bindingIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out string? name)
    {
        if (TryParseInvokeStaticIndexedPartNameBeforeBinding(seg, bindingIdx, boxIntLocals, out name))
        {
            return true;
        }

        if (TryFindPartNameLdcImmediatelyBeforeBinding(seg, bindingIdx, out name))
        {
            return true;
        }

        if (TryFindPartNameBeforeCubeListBuilderCreate(seg, bindingIdx, out name))
        {
            return true;
        }

        // Quadruped legs often reuse a mirrored CubeListBuilder: ldc childName, aload builder, PartPose, addOrReplaceChild.
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 32; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                continue;
            }

            for (var k = j + 1; k <= bindingIdx && k < j + 10; k++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[k], out _))
                {
                    name = sm.Groups[1].Value;
                    return true;
                }
            }
        }

        name = null;
        return false;
    }

    private static bool SegmentHasExplicitLdcPartNameBeforeBinding(List<string> seg, int bindingIdx)
    {
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 48; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                continue;
            }

            for (var k = j + 1; k <= bindingIdx && k < j + 12; k++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[k], out _) ||
                    seg[k].Contains("PartPose", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Skip loop unroll when the segment already names parts via ldc (quadruped legs, binding-site ids, etc.).
    /// </summary>
    private static bool SegmentHasExplicitPartNamingForLoopUnroll(List<string> seg)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                continue;
            }

            if (TryFindPartNameLdcImmediatelyBeforeBinding(seg, i, out _) ||
                TryFindPartNameBeforeCubeListBuilderCreate(seg, i, out _) ||
                SegmentHasExplicitLdcPartNameBeforeBinding(seg, i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// javac often pushes the mesh child name ldc immediately before <c>addOrReplaceChild</c> (no aload between).
    /// </summary>
    private static bool TryFindPartNameLdcImmediatelyBeforeBinding(List<string> seg, int bindingIdx, out string? name)
    {
        name = null;
        if (bindingIdx <= 0)
        {
            return false;
        }

        var minIdx = Math.Max(0, bindingIdx - PartNameLdcImmediateLookbackLines);
        for (var j = bindingIdx - 1; j >= minIdx; j--)
        {
            if (IsNamedOrObfuscatedMeshBindingNoiseBetweenPartNameAndInvoke(seg[j]))
            {
                continue;
            }

            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                if (seg[j].Contains("PartPose", StringComparison.Ordinal) ||
                    seg[j].Contains("invokestatic", StringComparison.Ordinal) ||
                    JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out _) ||
                    JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j], out _) ||
                    JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out _))
                {
                    continue;
                }

                break;
            }

            name = sm.Groups[1].Value;
            return true;
        }

        return false;
    }

    private static bool IsNamedOrObfuscatedMeshBindingNoiseBetweenPartNameAndInvoke(string line) =>
        JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(line, out _) ||
        line.Contains("dup", StringComparison.Ordinal) ||
        line.Contains("swap", StringComparison.Ordinal);

    private static bool TryFindPartNameBeforeCubeListBuilderCreate(List<string> seg, int maxLineIdx, out string? name)
    {
        name = null;
        for (var j = maxLineIdx - 1; j >= 0; j--)
        {
            if (!seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) &&
                !IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                continue;
            }

            for (var k = j - 1; k >= 0; k--)
            {
                var m = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]);
                if (m.Success)
                {
                    name = m.Groups[1].Value;
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private static string? FindFirstPartName(List<string> seg)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!seg[i].Contains("CubeListBuilder.create", StringComparison.Ordinal) &&
                !IsObfuscatedCubeListBuilderCreateLine(seg[i]))
            {
                continue;
            }

            for (var j = i - 1; j >= 0; j--)
            {
                var m = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// <c>javap -c</c> often wraps long <c>// Method …</c> comments across lines; merge non-instruction continuation
    /// lines so overload signatures (e.g. <c>CubeDeformation;FF</c>) stay visible for parsing.
    /// </summary>
    /// <summary>Consumes one reference operand pushed for <c>CubeDeformation</c>: aload/getstatic ZERO or inline <c>new CubeDeformation(f)</c>.</summary>
    private static void SkipCubeDeformationStackOperandsBackward(List<string> seg, ref int j, int minIdx)
    {
        while (j >= minIdx)
        {
            while (j >= minIdx && string.IsNullOrWhiteSpace(seg[j]))
            {
                j--;
            }

            if (j < minIdx)
            {
                return;
            }

            var ml = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (IsCubeDeformationExtendInvokeLine(ml, null))
            {
                j--;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out _))
            {
                j--;
                return;
            }

            if (ml.Contains("getstatic", StringComparison.Ordinal) &&
                ml.Contains("CubeDeformation", StringComparison.Ordinal) &&
                (ml.Contains("ZERO", StringComparison.Ordinal) || ml.Contains("NONE", StringComparison.Ordinal)))
            {
                j--;
                return;
            }

            if (ml.Contains("invokespecial", StringComparison.Ordinal) &&
                ml.Contains("<init>", StringComparison.Ordinal) &&
                ml.Contains("(F)V", StringComparison.Ordinal) &&
                ml.Contains("CubeDeformation", StringComparison.Ordinal))
            {
                if (j - 3 >= minIdx && JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j - 1], out _) &&
                    seg[j - 2].Contains("dup", StringComparison.Ordinal))
                {
                    var nm = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j - 3);
                    if (nm.Contains("new", StringComparison.Ordinal) &&
                        nm.Contains("CubeDeformation", StringComparison.Ordinal))
                    {
                        j -= 4;
                        return;
                    }
                }
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j], out _))
            {
                j--;
                continue;
            }

            break;
        }
    }

    private static bool TrySkipCubeDeformationOperandBackward(List<string> seg, ref int j, int minIdx)
    {
        var mark = j;
        SkipCubeDeformationStackOperandsBackward(seg, ref j, minIdx);
        return j < mark;
    }

    /// <summary>Parses Mojang texCrop overload stack right-to-left (UV crop ints, optional deformation, dimension ints, origin floats, quad ldc).</summary>
    private static bool TryParseStringQuadTexCropBoxBackward(List<string> seg, int startIdx, bool hasCubeDeformation,
        out double ox, out double oy, out double oz, out int dx, out int dy, out int dz, out int texUw, out int texUh,
        out string? quadKey, out int scanFrom)
    {
        ox = oy = oz = 0;
        dx = dy = dz = 0;
        texUw = texUh = 0;
        quadKey = null;
        scanFrom = startIdx;
        var minIdx = 0;
        var j = startIdx;
        if (!JavapBytecodeStreamAnalyzer.TryConsumeIntBackward(seg, ref j, minIdx, out texUh) ||
            !JavapBytecodeStreamAnalyzer.TryConsumeIntBackward(seg, ref j, minIdx, out texUw))
        {
            return false;
        }

        if (hasCubeDeformation && !TrySkipCubeDeformationOperandBackward(seg, ref j, minIdx))
        {
            return false;
        }

        if (!JavapBytecodeStreamAnalyzer.TryConsumeIntBackward(seg, ref j, minIdx, out dz) ||
            !JavapBytecodeStreamAnalyzer.TryConsumeIntBackward(seg, ref j, minIdx, out dy) ||
            !JavapBytecodeStreamAnalyzer.TryConsumeIntBackward(seg, ref j, minIdx, out dx))
        {
            return false;
        }

        if (!JavapBytecodeStreamAnalyzer.TryConsumeFloatBackward(seg, ref j, minIdx, out var fz) ||
            !JavapBytecodeStreamAnalyzer.TryConsumeFloatBackward(seg, ref j, minIdx, out var fy) ||
            !JavapBytecodeStreamAnalyzer.TryConsumeFloatBackward(seg, ref j, minIdx, out var fx))
        {
            return false;
        }

        if (!JavapBytecodeStreamAnalyzer.TryConsumeLdcStringBackward(seg, ref j, minIdx, out quadKey))
        {
            return false;
        }

        ox = fx;
        oy = fy;
        oz = fz;
        scanFrom = j;
        return true;
    }

    private static bool IsObfuscatedCubeListBuilderCreateLine(string line) =>
        line.Contains("invokestatic", StringComparison.Ordinal) &&
        line.Contains("// Method", StringComparison.Ordinal) &&
        Regex.IsMatch(line, @"//\s*Method\s+\w+\.\w+:\(\)L\w+;", RegexOptions.CultureInvariant);

    private static void SkipBooleanStackOperandBackward(List<string> lines, ref int j, int minIdx)
    {
        if (j < minIdx)
        {
            return;
        }

        var merged = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, j);
        if (merged.Contains("iconst_0", StringComparison.Ordinal) ||
            merged.Contains("iconst_1", StringComparison.Ordinal) ||
            JavapBytecodeStreamAnalyzer.TryParseIntLine(merged, out _))
        {
            j--;
        }
    }

    private static bool TryParseSixFloatsBackward(List<string> lines, int startIdx, int minIdx, string addBoxInvokeLine,
        IReadOnlyDictionary<int, double> boxFloatLocals,
        IReadOnlyDictionary<int, int> boxIntLocals,
        out double ox, out double oy, out double oz, out double sx, out double sy, out double sz, out int nextIdx,
        bool stringOverload, out string? mirrorQuadKey)
    {
        ox = oy = oz = sx = sy = sz = 0;
        nextIdx = startIdx;
        mirrorQuadKey = null;
        var j = startIdx;

        // Stack is …, box floats, CubeDeformation, texScaleF, texScaleF, addBox — consume tail floats before deformation.
        if (addBoxInvokeLine.Contains("CubeDeformation;FF)", StringComparison.Ordinal) ||
            addBoxInvokeLine.Contains("CubeDeformation;FF)L", StringComparison.Ordinal))
        {
            var skippedTexInflateFloats = 0;
            while (j >= minIdx && skippedTexInflateFloats < 2)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[j], out _))
                {
                    skippedTexInflateFloats++;
                }

                j--;
            }
        }

        if (addBoxInvokeLine.Contains("(FFFFFFZ)", StringComparison.Ordinal) ||
            addBoxInvokeLine.Contains("(FFFFFFZ)L", StringComparison.Ordinal))
        {
            SkipBooleanStackOperandBackward(lines, ref j, minIdx);
        }

        if (ContainsCubeDeformationDescriptor(addBoxInvokeLine, _maps))
        {
            SkipCubeDeformationStackOperandsBackward(lines, ref j, minIdx);
        }

        var floats = new List<double>();
        var addBoxOperandWarnings = new List<string>();
        while (j >= minIdx && floats.Count < 6)
        {
            if (lines[j].Contains("texOffs:(II)", StringComparison.Ordinal) ||
                JavapMeshBytecodeProfiles.IsNamedOrObfuscatedTexTwoIntsLine(lines[j]))
            {
                break;
            }

            var operandJ = j;
            if (TryConsumeOnePoseFloatOperandBackward(lines, ref operandJ, minIdx, depth: 0, boxFloatLocals,
                    boxIntLocals, addBoxOperandWarnings, out var exprFv))
            {
                floats.Add(exprFv);
                j = operandJ;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[j], out var fv))
            {
                floats.Add(fv);
                j--;
                continue;
            }

            if (TryParseFloatFromStaticMatrixBackward(lines, ref j, minIdx, boxIntLocals, out var matrixFv))
            {
                floats.Add(matrixFv);
                continue;
            }

            if (TryParseFloatFromComputedIntExprBackward(lines, ref j, minIdx, boxIntLocals, out var computedFv))
            {
                floats.Add(computedFv);
                continue;
            }

            if (TryParseFloatFromI2fBackward(lines, ref j, minIdx, boxIntLocals, out fv))
            {
                floats.Add(fv);
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var intAsFloat))
            {
                floats.Add(intAsFloat);
                j--;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[j], out var slot) && boxFloatLocals.TryGetValue(slot, out var localFv))
            {
                floats.Add(localFv);
                j--;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.IsBackwardStackNoiseLine(lines[j]))
            {
                j--;
                continue;
            }

            var merged = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, j);
            if (merged.Contains("invokevirtual", StringComparison.Ordinal) &&
                !merged.Contains("addBox", StringComparison.Ordinal))
            {
                j--;
                continue;
            }

            if (merged.Contains("invokestatic", StringComparison.Ordinal) &&
                !merged.Contains("addBox", StringComparison.Ordinal))
            {
                j--;
                continue;
            }

            j--;
        }

        if (floats.Count < 6)
        {
            return false;
        }

        if (stringOverload && j >= 0 && JavapBytecodeStreamAnalyzer.MatchLdcString(lines[j]) is { Success: true } sm)
        {
            mirrorQuadKey = sm.Groups[1].Value;
            j--;
        }

        floats.Reverse();
        ox = floats[0];
        oy = floats[1];
        oz = floats[2];
        sx = floats[3];
        sy = floats[4];
        sz = floats[5];
        nextIdx = j;
        return true;
    }

    private static bool TryParseFloatFromI2fBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out double fv)
    {
        var mark = j;
        if (JavapBytecodeStreamAnalyzer.TryParseIntToFloatOperandBackward(lines, ref j, minIdx, boxIntLocals, out fv))
        {
            return true;
        }

        j = mark;
        fv = 0;
        if (j < minIdx || !JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, j).Contains("i2f", StringComparison.Ordinal))
        {
            return false;
        }

        j--;
        while (j >= minIdx && JavapBytecodeStreamAnalyzer.IsBackwardStackNoiseLine(lines[j]))
        {
            j--;
        }

        if (j >= minIdx && lines[j].Contains("aaload", StringComparison.Ordinal))
        {
            j = mark;
            return false;
        }

        if (j >= minIdx && JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var iv))
        {
            fv = iv;
            j--;
            return true;
        }

        if (j >= minIdx && JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(lines[j], out var slot) &&
            boxIntLocals.TryGetValue(slot, out var localIv))
        {
            fv = localIv;
            j--;
            return true;
        }

        j = mark;
        return false;
    }

    /// <summary>
    /// Fallback when <see cref="TryParseTexOffsBackward"/> misses <c>iconst/bipush</c> pairs before
    /// <c>texOffs:(II)</c> on the same builder chain (e.g. <c>PlayerCapeModel.createCapeLayer</c> cape slab).
    /// </summary>
    private static bool TryParseTexOffsImmediatelyBeforeAddBox(List<string> seg, int addBoxLineIdx, out double u,
        out double v)
    {
        u = v = 0;
        var searchFrom = Math.Max(0, addBoxLineIdx - 32);
        for (var t = addBoxLineIdx - 1; t >= searchFrom; t--)
        {
            if (!seg[t].Contains("texOffs:(II)", StringComparison.Ordinal) &&
                !JavapMeshBytecodeProfiles.IsNamedOrObfuscatedTexTwoIntsLine(seg[t]))
            {
                continue;
            }

            var ints = new List<double>();
            for (var j = t - 1; j >= searchFrom && ints.Count < 2; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out var iv))
                {
                    ints.Add(iv);
                }
            }

            if (ints.Count < 2)
            {
                return false;
            }

            ints.Reverse();
            u = ints[0];
            v = ints[1];
            return true;
        }

        return false;
    }

    private static bool TryParseTexOffsBackward(List<string> lines, int startIdx, int floatMinIdx, out double u, out double v,
        out int nextIdx)
    {
        u = v = 0;
        nextIdx = startIdx;
        for (var t = startIdx; t >= floatMinIdx; t--)
        {
            if (!lines[t].Contains("texOffs:(II)", StringComparison.Ordinal) &&
                !JavapMeshBytecodeProfiles.IsNamedOrObfuscatedTexTwoIntsLine(lines[t]))
            {
                continue;
            }

            var ints = new List<double>();
            var j = t - 1;
            for (; j >= floatMinIdx && ints.Count < 2; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var iv))
                {
                    ints.Add(iv);
                }
            }

            if (ints.Count < 2)
            {
                return false;
            }

            ints.Reverse();
            u = ints[0];
            v = ints[1];
            nextIdx = j;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Walks the fluent <c>CubeListBuilder</c> chain from the prior <c>addBox</c> (or segment start) up to the current
    /// <c>addBox</c> line, applying <c>mirror()</c> / <c>mirror(boolean)</c> in source order (last call wins).
    /// </summary>
    private static bool ResolveMirrorUForCuboidFluentRegion(List<string> seg, int regionLow, int regionHigh)
    {
        if (regionLow > regionHigh)
        {
            return false;
        }

        var mirrorU = false;
        for (var i = regionLow; i <= regionHigh; i++)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMirrorNoArgFluentLine(seg[i]))
            {
                mirrorU = true;
                continue;
            }

            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMirrorBooleanFluentLine(seg[i]) &&
                TryParseBooleanLoadedImmediatelyBeforeInvoke(seg, i, out var z))
            {
                mirrorU = z;
            }
        }

        return mirrorU;
    }

    private static bool TryParseBooleanLoadedImmediatelyBeforeInvoke(List<string> seg, int invokeLine, out bool value)
    {
        value = false;
        for (var j = invokeLine - 1; j >= invokeLine - 12 && j >= 0; j--)
        {
            if (string.IsNullOrWhiteSpace(seg[j]))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out var iv) && iv is 0 or 1)
            {
                value = iv != 0;
                return true;
            }

            if (seg[j].Contains("iconst_1", StringComparison.Ordinal))
            {
                value = true;
                return true;
            }

            if (seg[j].Contains("iconst_0", StringComparison.Ordinal))
            {
                value = false;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.IsInsnIndexStartLine(seg[j]))
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// When javac emits <c>PartPose.ZERO.withScale(f)</c> or similar after <c>offset</c>/<c>offsetAndRotation</c>, lift a
    /// constant scale factor when it is loaded via <c>ldc</c>/<c>fconst_*</c> immediately before <c>withScale</c>.
    /// </summary>
    private static void TryApplyUniformScaleAfterPartPoseInvoke(List<string> seg, int partPoseInvokeLine, int minBound,
        int maxBound, JsonObject pose)
    {
        for (var k = partPoseInvokeLine + 1; k <= maxBound && k < seg.Count; k++)
        {
            if (!seg[k].Contains("withScale:(F)L", StringComparison.Ordinal) &&
                !seg[k].Contains("PartPose.withScale", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = k - 1; j >= minBound && j >= 0; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j], out var s))
                {
                    pose["uniformScale"] = Round(s);
                    return;
                }
            }

            return;
        }
    }

    private static bool IsPartPoseRotationInvokeLine(string line) =>
        line.Contains("PartPose.rotation", StringComparison.Ordinal);

    private static int FindMeshBindingLineIndex(List<string> seg)
    {
        for (var i = seg.Count - 1; i >= 0; i--)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                return i;
            }
        }

        return seg.Count - 1;
    }

    private static bool IsPartPoseFactoryInvokeLine(string line) =>
        line.Contains("PartPose.ZERO", StringComparison.Ordinal) ||
        line.Contains("PartPose.offsetAndRotation", StringComparison.Ordinal) ||
        (line.Contains("PartPose.offset", StringComparison.Ordinal) &&
         !line.Contains("offsetAndRotation", StringComparison.Ordinal)) ||
        IsPartPoseRotationInvokeLine(line);

    /// <summary>
    /// Reused <c>CubeListBuilder</c> legs (<c>ldc</c> name, <c>aload</c> builder, <c>PartPose</c>, bind) share a template
    /// <c>addBox</c> earlier in the slice — take the last <c>PartPose</c> factory immediately before this binding.
    /// </summary>
    private static bool TryParsePartPoseImmediatelyBeforeBinding(List<string> seg, string? poseClassHint,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        var bindingIdx = FindMeshBindingLineIndex(seg);
        if (bindingIdx <= 0)
        {
            return false;
        }

        var searchFrom = Math.Max(0, bindingIdx - 64);
        for (var i = bindingIdx - 1; i >= searchFrom; i--)
        {
            if (!IsPartPoseFactoryInvokeLine(seg[i]))
            {
                continue;
            }

            var poseWindowStart = ResolvePoseOperandWindowStart(seg, bindingIdx, i);
            if (TryParsePose(seg, poseWindowStart, i, poseClassHint, poseFloatLocals, poseIntLocals, poseWarnings,
                    out pose))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindPreviousMeshBindingLineIndex(List<string> seg, int beforeLine)
    {
        for (var j = beforeLine - 1; j >= 0; j--)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[j]))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>
    /// Keeps <c>PartPose</c> operand scans between the bind site and the last factory invoke — for reused
    /// <c>CubeListBuilder</c> legs, do not walk back into the shared template <c>addBox</c> float stack.
    /// </summary>
    private static int ResolvePoseOperandWindowStart(List<string> seg, int bindingIdx, int partPoseInvokeLine)
    {
        if (TryFindReusedBuilderAloadBeforeBinding(seg, bindingIdx, out _, out var templateAstoreLine))
        {
            for (var j = partPoseInvokeLine - 1; j > templateAstoreLine; j--)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out _))
                {
                    continue;
                }

                for (var k = j - 1; k >= templateAstoreLine && k > j - 6; k--)
                {
                    if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
                    {
                        return j;
                    }
                }
            }

            return Math.Max(templateAstoreLine + 1, partPoseInvokeLine - 12);
        }

        var prevBinding = FindPreviousMeshBindingLineIndex(seg, partPoseInvokeLine);
        return prevBinding >= 0 ? prevBinding + 1 : Math.Max(0, partPoseInvokeLine - 24);
    }

    private static void StampSetupAnimPivot(JsonObject pose)
    {
        if (pose["translation"] is not JsonArray t || t.Count < 3)
        {
            return;
        }

        pose["setupAnimPivot"] = new JsonArray(
            JsonValue.Create(t[0]!.GetValue<double>()),
            JsonValue.Create(t[1]!.GetValue<double>()),
            JsonValue.Create(t[2]!.GetValue<double>()));
    }

    private static void AppendPoseWarnings(JsonObject pose, IReadOnlyList<string> codes)
    {
        if (codes.Count == 0)
        {
            return;
        }

        var w = pose["liftWarnings"] as JsonArray ?? new JsonArray();
        foreach (var c in codes)
        {
            w.Add(c);
        }

        pose["liftWarnings"] = w;
    }

    private static bool TryParsePose(List<string> seg, int from, int to, string? poseClassHint,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        if (from > to)
        {
            return false;
        }

        if (SegmentContainsLoopBasedPoseMath(seg, from, to))
        {
            poseWarnings.Add("pose_loop_unsupported");
        }

        for (var i = from; i <= to; i++)
        {
            if (seg[i].Contains("PartPose.ZERO", StringComparison.Ordinal))
            {
                pose = ZeroPose();
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (seg[i].Contains("PartPose.offsetAndRotation", StringComparison.Ordinal))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 6, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var fl))
                {
                    continue;
                }

                fl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                    ["rotationEulerRad"] = new JsonArray { Round(fl[3]), Round(fl[4]), Round(fl[5]) },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (IsPartPoseRotationInvokeLine(seg[i]))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 3, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var rFl))
                {
                    continue;
                }

                rFl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { 0d, 0d, 0d },
                    ["rotationEulerRad"] = new JsonArray { Round(rFl[0]), Round(rFl[1]), Round(rFl[2]) },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (seg[i].Contains("PartPose.offset", StringComparison.Ordinal) &&
                !seg[i].Contains("offsetAndRotation", StringComparison.Ordinal))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 3, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var fl))
                {
                    continue;
                }

                fl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                    ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (!string.IsNullOrEmpty(poseClassHint) &&
                !string.Equals(poseClassHint, "PartPose", StringComparison.Ordinal))
            {
                _ = TryAnnotateObfuscatedPartPoseFactory(seg[i], poseClassHint, poseWarnings);
                if (TryParseObfuscatedPartPose(seg[i], poseClassHint, from, i, seg, poseFloatLocals, poseIntLocals,
                        poseWarnings, out pose))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseObfuscatedPartPose(string line, string poseShort, int minIdx, int invokeIdx,
        List<string> seg, IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        if (!line.Contains($"// Method {poseShort}.", StringComparison.Ordinal) &&
            !line.Contains($"// Method {poseShort}/", StringComparison.Ordinal))
        {
            return false;
        }

        _ = TryAnnotateObfuscatedPartPoseFactory(line, poseShort, poseWarnings);

        var six = $"(FFFFFF)L{poseShort};";
        var three = $"(FFF)L{poseShort};";
        if (line.Contains(six, StringComparison.Ordinal))
        {
            if (invokeIdx < 0 ||
                !TryParsePoseFloatOperandsBackward(seg, invokeIdx - 1, minIdx, 6, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var fl))
            {
                return false;
            }

            fl.Reverse();
            pose = new JsonObject
            {
                ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                ["rotationEulerRad"] = new JsonArray { Round(fl[3]), Round(fl[4]), Round(fl[5]) },
                ["eulerOrder"] = "XYZ"
            };
            var cap = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, cap, pose);
            return true;
        }

        if (line.Contains(three, StringComparison.Ordinal))
        {
            if (invokeIdx < 0 ||
                !TryParsePoseFloatOperandsBackward(seg, invokeIdx - 1, minIdx, 3, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var fl))
            {
                return false;
            }

            fl.Reverse();
            pose = new JsonObject
            {
                ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
                ["eulerOrder"] = "XYZ"
            };
            var cap3 = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, cap3, pose);
            return true;
        }

        if (line.Contains("getstatic", StringComparison.Ordinal) &&
            line.Contains($"Field {poseShort}.a:L{poseShort};", StringComparison.Ordinal))
        {
            pose = ZeroPose();
            var capZ = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, capZ, pose);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lifts JVM stack operands backward for vanilla <see cref="PartPose"/> factories: ldc/fconst values,
    /// <c>fneg</c>/<c>fadd</c>/<c>fmul</c>/<c>fsub</c>/<c>fdiv</c> of recursively parsed operands,
    /// <c>fload*</c> resolved from simple <c>fstore</c> constants when available, unknown <c>fload*</c> and
    /// <c>iload</c>+<c>isub</c>+<c>i2f</c>, and <c>dload*</c> placeholders as <c>0</c> when slots are unknown.
    /// </summary>
    private static bool TryParsePoseFloatOperandsBackward(List<string> seg, int start, int minIdx, int want,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out List<double> floats)
    {
        floats = new List<double>();
        var j = start;
        while (floats.Count < want && j >= minIdx)
        {
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth: 0, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var v))
            {
                return false;
            }

            floats.Add(v);
        }

        return floats.Count >= want;
    }

    private static bool TryConsumeOnePoseFloatOperandBackward(List<string> seg, ref int j, int minIdx, int depth,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out double v)
    {
        v = 0;
        if (depth > 12)
        {
            return false;
        }

        while (j >= minIdx && string.IsNullOrWhiteSpace(seg[j]))
        {
            j--;
        }

        if (j < minIdx)
        {
            return false;
        }

        var line = seg[j];
        if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(line, out v))
        {
            j--;
            return true;
        }

        if (TryParseKnownStaticFloatField(line, poseWarnings, out v))
        {
            j--;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseIntToFloatOperandBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseFloatFromStaticFloatArrayBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseFloatFromStaticMatrixBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseGuardianSpikeHelperInvokeBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        var mathMatch = JavapBytecodeStreamAnalyzer.MathUnaryFloatInvokeRegex().Match(line);
        if (mathMatch.Success)
        {
            var op = mathMatch.Groups[1].Success && mathMatch.Groups[1].Value.Length > 0
                ? mathMatch.Groups[1].Value
                : mathMatch.Groups[2].Value;
            var isDoubleUnary = line.Contains(":(D)D", StringComparison.Ordinal) ||
                                line.Contains("Mth.", StringComparison.Ordinal);
            j--;
            if (j >= minIdx && JavapBytecodeStreamAnalyzer.JavapD2iInsnRegex().IsMatch(seg[j]))
            {
                j--;
                if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(seg[j], out var angleSlot) &&
                    poseFloatLocals.TryGetValue(angleSlot, out var angleFv))
                {
                    j--;
                    v = op switch
                    {
                        "sin" => Math.Sin((int)angleFv),
                        "cos" => Math.Cos((int)angleFv),
                        _ => 0
                    };
                    return true;
                }
            }

            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var arg))
            {
                poseWarnings.Add("math_non_constant");
                return false;
            }

            v = op switch
            {
                "sin" => Math.Sin(isDoubleUnary ? arg : arg),
                "cos" => Math.Cos(isDoubleUnary ? arg : arg),
                "toRadians" => arg * Math.PI / 180.0,
                "abs" => Math.Abs(arg),
                _ => 0
            };
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapI2bInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var inner))
            {
                return false;
            }

            v = (sbyte)(byte)(int)inner;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapD2fInsnRegex().IsMatch(line))
        {
            j--;
            return TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth, poseFloatLocals, poseIntLocals,
                poseWarnings, out v);
        }

        if (JavapBytecodeStreamAnalyzer.JavapFnegInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var inner))
            {
                return false;
            }

            v = -inner;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFaddInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs + rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFmulInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs * rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFsubInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs - rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFdivInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            if (Math.Abs(rhs) < 1e-15)
            {
                return false;
            }

            v = lhs / rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(line, out var fslot))
        {
            j--;
            if (poseFloatLocals.TryGetValue(fslot, out v))
            {
                return true;
            }

            poseWarnings.Add("unknown_fload_zeroed");
            v = 0;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.IsDynamicDoublePlaceholderLoad(line))
        {
            j--;
            v = 0;
            return true;
        }

        // Skip aload / pop etc. noise without producing a usable scalar.
        var insn = line.TrimStart();
        if (Regex.IsMatch(insn, @"^\d+:\s+(pop|nop|swap|dup)\b", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(insn, @"^\d+:\s+aload_", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(insn, @"^\d+:\s+aload\s+\d+", RegexOptions.CultureInvariant))
        {
            j--;
            return TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth, poseFloatLocals, poseIntLocals, poseWarnings, out v);
        }

        return false;
    }

    /// <summary>
    /// Scans backward for <c>new CubeDeformation</c> + <c>dup</c> + float + <c>invokespecial &lt;init&gt;(F)V</c> (named javap only).
    /// </summary>
    private static bool TryReadUniformCubeDeformationCtorInvokespecialLine(List<string> seg, int searchFromInclusive,
        int minLineInclusive, MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        var winLow = minLineInclusive;
        for (var ctorIdx = searchFromInclusive; ctorIdx >= winLow; ctorIdx--)
        {
            var ml = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, ctorIdx);
            if (!ml.Contains("invokespecial", StringComparison.Ordinal) ||
                !ml.Contains("<init>", StringComparison.Ordinal) ||
                !ml.Contains("(F)V", StringComparison.Ordinal))
            {
                continue;
            }

            var isNamedDef = ml.Contains("CubeDeformation", StringComparison.Ordinal);
            var isObfDef = !isNamedDef && maps is not null &&
                           maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf) &&
                           JavapBytecodeStreamAnalyzer.ObfJavapLineReferencesShortType(ml, MojangMappingsParser.GetJavapClassArgForObfuscated(obf));
            if (!isNamedDef && !isObfDef)
            {
                continue;
            }

            var floatIdx = -1;
            var dupIdx = -1;
            var newIdx = -1;
            for (var j = ctorIdx - 1; j >= winLow; j--)
            {
                var jl = seg[j];
                if (floatIdx < 0 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(jl, out _))
                {
                    floatIdx = j;
                    continue;
                }

                if (floatIdx >= 0 && dupIdx < 0 && jl.Contains("dup", StringComparison.Ordinal))
                {
                    dupIdx = j;
                    continue;
                }

                if (newIdx < 0 && jl.Contains("new", StringComparison.Ordinal))
                {
                    newIdx = j;
                    if (dupIdx >= 0)
                    {
                        break;
                    }
                }
            }

            if (floatIdx >= 0 && newIdx >= 0 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[floatIdx], out inflate))
            {
                var newLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, newIdx);
                if (newLine.Contains("CubeDeformation", StringComparison.Ordinal) || isObfDef)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <c>inflate</c> from inline <c>new CubeDeformation(f)</c> before <c>addBox</c>, or from an <c>aload</c> slot
    /// mapped by <see cref="BuildCubeDeformationInflateByRefLocalSlot"/>.
    /// </summary>
    private static bool TryResolveUniformCubeDeformationInflate(List<string> seg, int addBoxInvokeLineIdx,
        int minLineInclusive, IReadOnlyDictionary<int, double> cubeDeformationInflateByRefSlot,
        MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        if (addBoxInvokeLineIdx <= 0)
        {
            return false;
        }

        var boxLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, addBoxInvokeLineIdx);
        if (!ContainsCubeDeformationDescriptor(boxLine, maps))
        {
            return false;
        }

        var preInv = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, addBoxInvokeLineIdx - 1).TrimStart();
        if (Regex.IsMatch(preInv, @"^\d+:\s+aload_", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(preInv, @"^\d+:\s+aload\s+\d+", RegexOptions.CultureInvariant))
        {
            return JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[addBoxInvokeLineIdx - 1], out var slot) &&
                   cubeDeformationInflateByRefSlot.TryGetValue(slot, out inflate);
        }

        var winLow = Math.Max(minLineInclusive, addBoxInvokeLineIdx - 18);
        return TryReadUniformCubeDeformationCtorInvokespecialLine(seg, addBoxInvokeLineIdx - 1, winLow, maps, out inflate);
    }

    private static bool IsObfuscatedCubeDeformationGetStatic(string getStaticLine, MojangMappingsParser maps)
    {
        if (!getStaticLine.Contains("getstatic", StringComparison.Ordinal))
        {
            return false;
        }

        if (!maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf))
        {
            return false;
        }

        var shortName = MojangMappingsParser.GetJavapClassArgForObfuscated(obf);
        return getStaticLine.Contains($"L{shortName};", StringComparison.Ordinal);
    }

    private static bool ContainsCubeDeformationDescriptor(string mergedInvokeLine, MojangMappingsParser? maps)
    {
        if (mergedInvokeLine.Contains("CubeDeformation", StringComparison.Ordinal))
        {
            return true;
        }

        if (maps is null)
        {
            return false;
        }

        if (!maps.TryGetObfuscated(
                "net.minecraft.client.model.geom.builders.CubeDeformation",
                out var obf))
        {
            return false;
        }

        var shortName = MojangMappingsParser.GetJavapClassArgForObfuscated(obf);
        return mergedInvokeLine.Contains($"L{shortName};", StringComparison.Ordinal);
    }

    private static JsonObject ZeroPose() =>
        new()
        {
            ["translation"] = new JsonArray { 0d, 0d, 0d },
            ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
            ["eulerOrder"] = "XYZ"
        };

}
