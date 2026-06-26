using System.Text.Json.Nodes;
using static AutoPBR.Tools.GeometryCompiler.GeometryLiftCoordinateRounding;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
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
            var mergedLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, i);
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedFloatAddBoxLine(mergedLine, out _))
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
            int? uvSpanD = null;
            int? texCropU = null;
            int? texCropV = null;
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
                    texCropU = tu;
                    texCropV = tv;
                    uvSpanW = dx;
                    uvSpanH = dy;
                    uvSpanD = dz;
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
                        texCropU = tcropU;
                        texCropV = tcropV;
                        uvSpanW = tdx;
                        uvSpanH = tdy;
                        uvSpanD = tdz;
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
                u = texCropU ?? 0;
                v = texCropV ?? 0;
            }
            else if (!TryParseTexOffsFromStaticMatrixBackward(seg, scanFrom,
                         bi > 0 ? addBoxIndices[bi - 1] + 1 : 0, boxIntLocals, out u, out v, out scanFrom) &&
                     !TryParseTexOffsBackward(seg, scanFrom,
                         floatMinIdx: bi > 0 ? addBoxIndices[bi - 1] + 1 : 0, boxIntLocals, out u, out v, out scanFrom) &&
                     !TryParseTexOffsImmediatelyBeforeAddBox(seg, lineIdx, boxIntLocals, out u, out v))
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
                var addBoxLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, lineIdx);
                obfDef = !ContainsCubeDeformationDescriptor(addBoxLine, _maps);
            }

            var warnings = BuildCuboidLiftWarnings(liftKind, maskResult, obfDef);
            faceMaskList = GeometryLiftDegenerateFaceMask.ApplyForLift(faceMaskList, sx, sy, sz);
            var c = CreateCuboidJson(from, to, uv, textureKey, provenanceCube, liftKind, warnings,
                faceMaskList, mirrorU, uvSpanW, uvSpanH, uvSpanD, inflateAmt);

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
}
