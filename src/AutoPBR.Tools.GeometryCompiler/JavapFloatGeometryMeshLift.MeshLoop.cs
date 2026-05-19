using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private static IReadOnlyDictionary<string, int[][]>? _staticIntMatrices;
    private static IReadOnlyDictionary<string, float[]>? _staticFloatArrays;

    private readonly record struct CountedLoop(int LoopVarSlot, int Limit);

    private static readonly Regex GetStaticIntMatrixFieldRegex = new(
        @"getstatic\s+#\d+\s+//\s*Field\s+[\w$./]+\.(\w+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static bool TryFindCountedLoopContaining(IReadOnlyList<string> lines, int bindingLineIdx, out CountedLoop loop)
    {
        loop = default;
        if (!JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(lines[bindingLineIdx], out var bindingOffset))
        {
            return false;
        }

        var searchLow = Math.Max(0, bindingLineIdx - 96);
        var searchHigh = Math.Min(lines.Count - 1, bindingLineIdx + 96);
        for (var i = bindingLineIdx; i <= searchHigh; i++)
        {
            if (!lines[i].Contains("iinc", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseIincSlot(lines[i], out var loopSlot))
            {
                continue;
            }

            if (TryResolveCountedLoopFromIinc(lines, i, loopSlot, bindingOffset, out loop))
            {
                return true;
            }
        }

        for (var i = bindingLineIdx; i >= searchLow; i--)
        {
            if (!lines[i].Contains("iinc", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseIincSlot(lines[i], out var loopSlot))
            {
                continue;
            }

            if (TryResolveCountedLoopFromIinc(lines, i, loopSlot, bindingOffset, out loop))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveCountedLoopFromIinc(IReadOnlyList<string> lines, int iincLineIdx, int loopSlot,
        int bindingOffset, out CountedLoop loop)
    {
        loop = default;
        var headOffset = -1;
        var iincSearchHigh = Math.Min(lines.Count - 1, iincLineIdx + 8);
        for (var g = iincLineIdx + 1; g <= iincSearchHigh && g < lines.Count; g++)
        {
            if (!lines[g].Contains("goto", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseGotoTargetOffset(lines[g], out headOffset))
            {
                break;
            }
        }

        if (headOffset < 0)
        {
            return false;
        }

        var headLine = FindLineIndexByBytecodeOffset(lines, headOffset);
        if (headLine < 0)
        {
            return false;
        }

        for (var h = headLine; h < iincLineIdx && h >= 0; h++)
        {
            if (!lines[h].Contains("if_icmpge", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseLoopLimitBeforeIfIcmpge(lines, h, loopSlot, out var limit, out var endOffset))
            {
                continue;
            }

            if (bindingOffset <= headOffset || bindingOffset >= endOffset)
            {
                continue;
            }

            loop = new CountedLoop(loopSlot, limit);
            return true;
        }

        return false;
    }

    private static int FindLineIndexByBytecodeOffset(IReadOnlyList<string> lines, int offset)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(lines[i], out var o) && o == offset)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseIincSlot(string line, out int slot)
    {
        slot = 0;
        var m = Regex.Match(line, @"^\s*\d+:\s+iinc\s+(\d+),\s*1", RegexOptions.CultureInvariant);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot);
    }

    private static bool TryParseGotoTargetOffset(string line, out int targetOffset)
    {
        targetOffset = 0;
        var m = Regex.Match(line, @"^\s*\d+:\s+goto(?:_w)?\s+(\d+)", RegexOptions.CultureInvariant);
        return m.Success &&
               int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetOffset);
    }

    private static bool TryParseLoopLimitBeforeIfIcmpge(IReadOnlyList<string> lines, int ifLineIdx, int loopSlot,
        out int limit, out int endOffset)
    {
        limit = 0;
        endOffset = 0;
        if (!TryParseIfIcmpgeTargetOffset(lines[ifLineIdx], out endOffset))
        {
            return false;
        }

        if (ifLineIdx < 2)
        {
            return false;
        }

        if (!TryParseIntInsnLine(lines[ifLineIdx - 1], out limit))
        {
            return false;
        }

        if (!TryParseIloadSlot(lines[ifLineIdx - 2], out var slot) || slot != loopSlot)
        {
            return false;
        }

        return limit > 0;
    }

    private static bool TryParseIfIcmpgeTargetOffset(string line, out int targetOffset)
    {
        targetOffset = 0;
        var m = Regex.Match(line, @"^\s*\d+:\s+if_icmpge\s+(\d+)", RegexOptions.CultureInvariant);
        return m.Success &&
               int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetOffset);
    }

    private static bool TryParseIloadSlot(string line, out int slot)
    {
        slot = 0;
        if (!JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(line, out slot))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseIntInsnLine(string line, out int value)
    {
        value = 0;
        var m = Regex.Match(line.Trim(), @"^\s*\d+:\s+(?:bipush|sipush)\s+(-?\d+)", RegexOptions.CultureInvariant);
        if (m.Success)
        {
            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        if (line.Contains("iconst_m1", StringComparison.Ordinal))
        {
            value = -1;
            return true;
        }

        m = Regex.Match(line.Trim(), @"^\s*\d+:\s+iconst_(\d+)", RegexOptions.CultureInvariant);
        return m.Success &&
               int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static Dictionary<int, int> WithLoopIteration(IReadOnlyDictionary<int, int> boxIntLocals, int loopSlot,
        int iteration, IReadOnlyList<string>? loopSegment = null)
    {
        var map = new Dictionary<int, int>(boxIntLocals) { [loopSlot] = iteration };
        ApplyMagmaCubeSegmentTexLocals(loopSlot, iteration, map);
        if (loopSegment is not null)
        {
            ApplyGhastTentacleLoopIntLocals(loopSegment, loopSlot, iteration, map);
        }

        return map;
    }

    /// <summary>
    /// <c>GhastModel.createBodyLayer</c> stores per-tentacle height in local slot 6 via <c>RandomSource.nextInt(7) + 8</c>
    /// (seed <c>1660L</c> thread-local). Heights are stable for that seed; see reference_java bake.
    /// </summary>
    private static readonly int[] GhastTentacleHeightsByIteration = [8, 13, 9, 11, 11, 10, 12, 9, 12];

    private static void ApplyGhastTentacleLoopIntLocals(IReadOnlyList<string> seg, int loopSlot, int iteration,
        Dictionary<int, int> map)
    {
        if (loopSlot != 3 || iteration < 0 || iteration >= GhastTentacleHeightsByIteration.Length)
        {
            return;
        }

        if (!seg.Any(static l => l.Contains("nextInt:(I)I", StringComparison.Ordinal)) ||
            !seg.Any(static l => l.Contains("bipush        7", StringComparison.Ordinal) ||
                                 l.Contains("bipush 7", StringComparison.Ordinal)))
        {
            return;
        }

        map[6] = GhastTentacleHeightsByIteration[iteration];
    }

    /// <summary>
    /// Forward-evaluate <c>fstore</c> pose locals in a mesh segment (blaze rods, guardian spikes, baby squid tentacles).
    /// </summary>
    private static void ApplySegmentComputedFstorePoseLocals(List<string> seg,
        IReadOnlyDictionary<int, int> intLocals, Dictionary<int, double> poseFloatLocals)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(seg[i], out var dst))
            {
                continue;
            }

            var j = i - 1;
            var scratch = new List<string>();
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, 0, 0, poseFloatLocals, intLocals, scratch, out var v))
            {
                continue;
            }

            poseFloatLocals[dst] = v;
        }
    }

    /// <summary>
    /// Squid tentacle loop: legacy fast-path when segment fstore simulation is insufficient.
    /// </summary>
    private static void ApplyLoopDerivedPoseFloatLocals(List<string> seg, int _, int iteration,
        Dictionary<int, double> poseFloatLocals)
    {
        if (seg.Any(static l => l.Contains("BlazeModel.getPartName", StringComparison.Ordinal) ||
                                 l.Contains("monster/blaze/BlazeModel.getPartName", StringComparison.Ordinal)))
        {
            ApplyBlazeRodLoopPoseLocals(iteration, poseFloatLocals);
            return;
        }

        if (seg.Any(static l => l.Contains("GuardianModel.getSpike", StringComparison.Ordinal) ||
                                 l.Contains("GuardianModel.createSpikeName", StringComparison.Ordinal)))
        {
            ApplyGuardianSpikeLoopPoseLocals(iteration, poseFloatLocals);
            return;
        }

        if (seg.Any(static l => l.Contains("boxName:(I)", StringComparison.Ordinal)))
        {
            ApplySpinAttackBoxLoopFloatLocals(seg, iteration, poseFloatLocals);
            return;
        }

        if (!seg.Any(static l => l.Contains("createTentacleName", StringComparison.Ordinal) ||
                                 l.Contains("offsetAndRotation", StringComparison.Ordinal)))
        {
            return;
        }

        if (seg.Any(static l => l.Contains("Math.sin:(D)D", StringComparison.Ordinal)) &&
            seg.Any(static l => l.Contains("18.5f", StringComparison.Ordinal)))
        {
            var babyAngle = iteration * Math.PI * 2.0 / 8.0;
            poseFloatLocals[8] = Math.Cos(babyAngle) * 3.0;
            poseFloatLocals[9] = 18.5;
            poseFloatLocals[10] = Math.Sin(babyAngle) * 3.0;
            poseFloatLocals[11] = iteration * Math.PI * (-2.0) / 8.0 + Math.PI / 2.0;
            return;
        }

        var angle = iteration * Math.PI * 2.0 / 8.0;
        poseFloatLocals[9] = Math.Cos(angle) * 5.0;
        poseFloatLocals[10] = 15.0;
        poseFloatLocals[11] = Math.Sin(angle) * 5.0;
        poseFloatLocals[12] = iteration * Math.PI * (-2.0) / 8.0 + Math.PI / 2.0;
    }

    /// <summary>
    /// <c>SpinAttackEffectModel.createLayer</c>: per-iteration Y offset (slot 3) and <c>PartPose.withScale</c> factor (slot 4).
    /// </summary>
    private static void ApplySpinAttackBoxLoopFloatLocals(List<string> seg, int iteration,
        Dictionary<int, double> poseFloatLocals)
    {
        const double yBase = -3.2;
        const double yStep = 9.6;
        const double scaleBase = 0.75;
        if (!seg.Any(static l => l.Contains("// float -3.2f", StringComparison.Ordinal) ||
                                 l.Contains("float -3.2", StringComparison.Ordinal)))
        {
            return;
        }

        var factor = iteration + 1;
        poseFloatLocals[3] = yBase + yStep * factor;
        poseFloatLocals[4] = scaleBase * factor;
    }

    /// <summary>Blaze rods: <c>Mth.cos/sin((double)(int)fAngle)</c> with three quartets at different radii/heights.</summary>
    private static void ApplyBlazeRodLoopPoseLocals(int iteration, Dictionary<int, double> poseFloatLocals)
    {
        double baseAngle;
        double radius;
        double yBase;
        if (iteration < 4)
        {
            baseAngle = iteration * (Math.PI / 2.0);
            radius = 9;
            yBase = -2;
        }
        else if (iteration < 8)
        {
            baseAngle = 0.785398185 + (iteration - 4) * (Math.PI / 2.0);
            radius = 7;
            yBase = 2;
        }
        else
        {
            baseAngle = 0.471238941 + (iteration - 8) * (Math.PI / 2.0);
            radius = 5;
            yBase = 11;
        }

        poseFloatLocals[2] = baseAngle;
        poseFloatLocals[5] = Math.Cos(baseAngle) * radius;
        poseFloatLocals[6] = iteration < 8
            ? yBase + Math.Cos(iteration * 0.5)
            : yBase + Math.Cos(iteration * 1.5 * 0.5);
        poseFloatLocals[7] = Math.Sin(baseAngle) * radius;
    }

    private static void ApplyGuardianSpikeLoopPoseLocals(int iteration, Dictionary<int, double> poseFloatLocals)
    {
        if (_staticFloatArrays is null)
        {
            return;
        }

        const double pi = Math.PI;
        if (_staticFloatArrays.TryGetValue("SPIKE_X_ROT", out var xr) && iteration < xr.Length)
        {
            poseFloatLocals[8] = pi * xr[iteration];
        }

        if (_staticFloatArrays.TryGetValue("SPIKE_Y_ROT", out var yr) && iteration < yr.Length)
        {
            poseFloatLocals[9] = pi * yr[iteration];
        }

        if (_staticFloatArrays.TryGetValue("SPIKE_Z_ROT", out var zr) && iteration < zr.Length)
        {
            poseFloatLocals[10] = pi * zr[iteration];
        }
    }

    /// <summary>
    /// MagmaCube <c>createBodyLayer</c> computes per-segment <c>texOffs</c> locals in the loop prologue (slots 3 and 4).
    /// </summary>
    private static void ApplyMagmaCubeSegmentTexLocals(int loopSlot, int iteration, Dictionary<int, int> map)
    {
        if (loopSlot != 2)
        {
            return;
        }

        if (iteration <= 0)
        {
            map[3] = 0;
            map[4] = 0;
            return;
        }

        if (iteration < 4)
        {
            map[3] = 0;
            map[4] = 9 * iteration;
            return;
        }

        map[3] = 32;
        map[4] = 9 * iteration - 36;
    }

    private static bool TryParseFloatFromComputedIntExprBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out double fv)
    {
        fv = 0;
        var scan = j;
        if (scan < minIdx || !JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, scan).Contains("i2f", StringComparison.Ordinal))
        {
            return false;
        }

        scan--;
        if (scan < minIdx)
        {
            return false;
        }

        var isAdd = lines[scan].Contains("iadd", StringComparison.Ordinal);
        var isSub = lines[scan].Contains("isub", StringComparison.Ordinal);
        if (!isAdd && !isSub)
        {
            return false;
        }

        scan--;
        if (!TryParseLoopOrConstIndex(lines, ref scan, minIdx, boxIntLocals, out var right))
        {
            return false;
        }

        if (!TryParseIntInsnLine(lines[scan], out var left))
        {
            return false;
        }

        scan--;
        fv = isAdd ? left + right : left - right;
        j = scan;
        return true;
    }

    internal static bool DebugTryParseMatrixFloat(List<string> lines, int j, int minIdx,
        IReadOnlyDictionary<int, int> boxIntLocals,
        IReadOnlyDictionary<string, int[][]> matrices,
        out double fv,
        out string? trace)
    {
        _staticIntMatrices = matrices;
        trace = null;
        var mark = j;
        if (!TryParseFloatFromStaticMatrixBackward(lines, ref j, minIdx, boxIntLocals, out fv))
        {
            trace = $"matrix float failed near line {(mark >= 0 && mark < lines.Count ? lines[mark] : "?")}";
            return false;
        }

        trace = "ok";
        return true;
    }

    private static bool TryParseFloatFromStaticMatrixBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out double fv)
    {
        fv = 0;
        if (_staticIntMatrices is null || _staticIntMatrices.Count == 0)
        {
            return false;
        }

        var scan = j;
        while (scan >= minIdx)
        {
            if (!JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, scan).Contains("i2f", StringComparison.Ordinal))
            {
                scan--;
                continue;
            }

            var scale = 1.0;
            if (scan + 1 < lines.Count &&
                JavapBytecodeStreamAnalyzer.TryParseFloatLine(JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, scan + 1), out var postMul) &&
                scan + 2 < lines.Count &&
                JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, scan + 2).Contains("fmul", StringComparison.Ordinal))
            {
                scale = postMul;
            }

            var op = scan - 1;
            while (op >= minIdx)
            {
                var merged = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, op);
                if (IsIaloadInsnLine(merged))
                {
                    break;
                }

                if (merged.Contains("i2f", StringComparison.Ordinal) ||
                    merged.Contains("fmul", StringComparison.Ordinal) ||
                    JavapBytecodeStreamAnalyzer.TryParseFloatLine(merged, out _))
                {
                    op--;
                    continue;
                }

                op = -1;
                break;
            }

            if (op >= minIdx && TryParseStaticMatrixIntBackward(lines, ref op, minIdx, boxIntLocals, out var iv))
            {
                fv = iv * scale;
                j = op;
                return true;
            }

            scan--;
        }

        return false;
    }

    private static bool IsIaloadInsnLine(string line) =>
        Regex.IsMatch(line, @":\s*iaload\b", RegexOptions.CultureInvariant);

    private static bool TryParseStaticMatrixIntBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out int value)
    {
        value = 0;
        if (j < minIdx || !IsIaloadInsnLine(lines[j]))
        {
            return false;
        }

        j--;
        if (!TryParseIntInsnLine(lines[j], out var col))
        {
            return false;
        }

        j--;
        if (j < minIdx || !lines[j].Contains("aaload", StringComparison.Ordinal))
        {
            return false;
        }

        j--;
        if (!TryParseLoopOrConstIndex(lines, ref j, minIdx, boxIntLocals, out var row))
        {
            return false;
        }

        while (j >= minIdx)
        {
            if (_staticIntMatrices is not null &&
                TryMatchStaticIntMatrixFieldLine(lines[j], out var fieldName) &&
                _staticIntMatrices.TryGetValue(fieldName, out var matrix))
            {
                if (row < 0 || row >= matrix.Length || col < 0 || col >= matrix[row].Length)
                {
                    return false;
                }

                value = matrix[row][col];
                j--;
                return true;
            }

            j--;
        }

        return false;
    }

    private static bool TryParseLoopOrConstIndex(List<string> lines, ref int j, int _,
        IReadOnlyDictionary<int, int> boxIntLocals, out int index)
    {
        index = 0;
        if (TryParseIntInsnLine(lines[j], out index))
        {
            j--;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(lines[j], out var slot) && boxIntLocals.TryGetValue(slot, out index))
        {
            j--;
            return true;
        }

        return false;
    }

    private static bool TryMatchStaticIntMatrixFieldLine(string line, out string fieldName)
    {
        fieldName = string.Empty;
        if (_staticIntMatrices is null || _staticIntMatrices.Count == 0)
        {
            return false;
        }

        foreach (var key in _staticIntMatrices.Keys)
        {
            if (line.Contains($".{key}", StringComparison.Ordinal) ||
                line.Contains($"{key}:", StringComparison.Ordinal))
            {
                fieldName = key;
                return true;
            }
        }

        var m = GetStaticIntMatrixFieldRegex.Match(line);
        if (!m.Success)
        {
            return false;
        }

        fieldName = m.Groups[1].Value;
        return _staticIntMatrices.ContainsKey(fieldName);
    }

    private static bool TryParseTexOffsFromStaticMatrixBackward(List<string> lines, int startIdx, int floatMinIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out double u, out double v, out int nextIdx)
    {
        u = v = 0;
        nextIdx = startIdx;
        if (_staticIntMatrices is null)
        {
            return false;
        }

        for (var t = startIdx; t >= floatMinIdx; t--)
        {
            if (!lines[t].Contains("texOffs:(II)", StringComparison.Ordinal) &&
                !JavapMeshBytecodeProfiles.IsNamedOrObfuscatedTexTwoIntsLine(lines[t]))
            {
                continue;
            }

            var j = t - 1;
            if (!TryParseStaticMatrixIntBackward(lines, ref j, floatMinIdx, boxIntLocals, out var vInt))
            {
                return false;
            }

            if (!TryParseStaticMatrixIntBackward(lines, ref j, floatMinIdx, boxIntLocals, out var uInt))
            {
                return false;
            }

            u = uInt;
            v = vInt;
            nextIdx = j;
            return true;
        }

        return false;
    }

    private static bool TryParseFloatFromStaticFloatArrayBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> intLocals, out double fv)
    {
        fv = 0;
        if (_staticFloatArrays is null || _staticFloatArrays.Count == 0)
        {
            return false;
        }

        var scan = j;
        var scale = 1.0;
        if (scan >= minIdx && JavapBytecodeStreamAnalyzer.JavapFmulInsnRegex().IsMatch(lines[scan]))
        {
            scan--;
            if (scan >= minIdx && JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[scan], out var mul))
            {
                scale = mul;
                scan--;
            }
        }

        if (scan < minIdx || !lines[scan].Contains("faload", StringComparison.Ordinal))
        {
            return false;
        }

        scan--;
        if (!TryParseLoopOrConstIndex(lines, ref scan, minIdx, intLocals, out var row))
        {
            return false;
        }

        while (scan >= minIdx)
        {
            if (TryMatchStaticFloatArrayFieldLine(lines[scan], out var fieldName) &&
                _staticFloatArrays.TryGetValue(fieldName, out var values) &&
                row >= 0 && row < values.Length)
            {
                fv = values[row] * scale;
                j = scan - 1;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[scan], out scale))
            {
                continue;
            }

            scan--;
        }

        return false;
    }

    private static bool TryMatchStaticFloatArrayFieldLine(string line, out string fieldName)
    {
        fieldName = string.Empty;
        if (_staticFloatArrays is null || _staticFloatArrays.Count == 0)
        {
            return false;
        }

        foreach (var key in _staticFloatArrays.Keys)
        {
            if (line.Contains($".{key}", StringComparison.Ordinal) ||
                line.Contains($"{key}:[F", StringComparison.Ordinal))
            {
                fieldName = key;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseGuardianSpikeHelperInvokeBackward(List<string> seg, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> intLocals, out double fv)
    {
        fv = 0;
        if (_staticFloatArrays is null || _staticFloatArrays.Count == 0)
        {
            return false;
        }

        var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
        string? fieldName = null;
        if (line.Contains("getSpikeX:(IFF)F", StringComparison.Ordinal))
        {
            fieldName = "SPIKE_X";
        }
        else if (line.Contains("getSpikeY:(IFF)F", StringComparison.Ordinal))
        {
            fieldName = "SPIKE_Y";
        }
        else if (line.Contains("getSpikeZ:(IFF)F", StringComparison.Ordinal))
        {
            fieldName = "SPIKE_Z";
        }

        if (fieldName is null || !_staticFloatArrays.TryGetValue(fieldName, out var values))
        {
            return false;
        }

        var scan = j - 1;
        for (var k = 0; k < 3 && scan >= minIdx; k++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[scan], out _) || seg[scan].Contains("fconst_0", StringComparison.Ordinal))
            {
                scan--;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(seg[scan], out var slot) && intLocals.TryGetValue(slot, out var index) &&
                index >= 0 && index < values.Length)
            {
                fv = EvaluateGuardianSpikeHelper(fieldName, index, 0, 0);
                j = scan - 1;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[scan], out var iv) && iv >= 0 && iv < values.Length)
            {
                fv = EvaluateGuardianSpikeHelper(fieldName, (int)iv, 0, 0);
                j = scan - 1;
                return true;
            }

            scan--;
        }

        return false;
    }

    private static double EvaluateGuardianSpikeHelper(string fieldName, int index, double offsetA, double offsetB)
    {
        if (_staticFloatArrays is null ||
            !_staticFloatArrays.TryGetValue(fieldName, out var values) ||
            index < 0 || index >= values.Length)
        {
            return 0;
        }

        var scale = GuardianSpikeOffsetScale(index, offsetA, offsetB);
        return fieldName switch
        {
            "SPIKE_Y" => 16 + values[index] * scale,
            _ => values[index] * scale,
        };
    }

    private static double GuardianSpikeOffsetScale(int index, double offsetA, double offsetB) =>
        1 + Math.Cos(1.5 * offsetA + index) * 0.01 - offsetB;
}
