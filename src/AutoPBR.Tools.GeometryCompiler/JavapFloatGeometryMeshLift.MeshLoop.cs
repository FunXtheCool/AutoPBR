using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private static IReadOnlyDictionary<string, int[][]>? _staticIntMatrices;
    private static IReadOnlyDictionary<string, float[]>? _staticFloatArrays;

    private readonly record struct CountedLoop(int LoopVarSlot, int Limit);

    private static readonly Regex GetStaticIntMatrixFieldRegex = new(
        @"getstatic\s+#\d+\s+//\s*Field\s+(?:[\w$./]+\.)?(\w+)",
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
}
