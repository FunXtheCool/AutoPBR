using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
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
