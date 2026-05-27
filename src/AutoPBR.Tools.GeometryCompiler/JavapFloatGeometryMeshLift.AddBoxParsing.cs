using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
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
}
