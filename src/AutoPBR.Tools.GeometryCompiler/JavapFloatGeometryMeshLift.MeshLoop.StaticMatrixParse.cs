using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
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
}
