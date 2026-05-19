using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Parses and normalizes <c>javap -c</c> bytecode text: line folding, instruction regex matching,
/// local-slot extraction, and backward stack-operand scanning for mesh lift.
/// </summary>
internal static partial class JavapBytecodeStreamAnalyzer
{
    [GeneratedRegex(@"^\s*\d+:\s+", RegexOptions.CultureInvariant)]
    private static partial Regex JavapInsnIndexStartRegex();

    [GeneratedRegex(@"^\s*\d+:\s+ldc\s+#\d+\s+//\s*String\s+(\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex LdcStringRegex();

    [GeneratedRegex(@"^\s*\d+:\s+ldc\s+#\d+\s+//\s*float\s+(-?[\d.]+)f", RegexOptions.CultureInvariant)]
    private static partial Regex LdcFloatRegex();

    [GeneratedRegex(@"^\s*\d+:\s+ldc\s+#\d+\s+//\s*int\s+(-?\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LdcIntRegex();

    [GeneratedRegex(@"^\s*\d+:\s+bipush\s+(-?\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex BipushRegex();

    [GeneratedRegex(@"^\s*\d+:\s+sipush\s+(-?\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex SipushRegex();

    [GeneratedRegex(@"^\s*\d+:\s+iconst_m1", RegexOptions.CultureInvariant)]
    private static partial Regex IconstM1Regex();

    [GeneratedRegex(@"^\s+\d+:\s+aload(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapAloadLocalSlotRegex();

    [GeneratedRegex(@"^\s+\d+:\s+astore(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapAstoreLocalSlotRegex();

    [GeneratedRegex(@"^\s+\d+:\s+fstore(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapFstoreLocalSlotRegex();

    [GeneratedRegex(@"^\s+\d+:\s+fload(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapFloadLocalSlotRegex();

    [GeneratedRegex(@"^\s+\d+:\s+iload(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapIloadLocalSlotRegex();

    [GeneratedRegex(@"^\s+\d+:\s+istore(?:_(\d+)|\s+(\d+))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavapIstoreLocalSlotRegex();

    [GeneratedRegex(@"^\s*\d+:\s+fadd\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapFaddInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+fmul\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapFmulInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+fsub\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapFsubInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+fdiv\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapFdivInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+fneg\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapFnegInsnRegex();

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:java/lang/Math\.(sin|cos|toRadians|abs):\([FD]\)[FD]|net/minecraft/util/Mth\.(sin|cos):\(D\)F)",
        RegexOptions.CultureInvariant)]
    internal static partial Regex MathUnaryFloatInvokeRegex();

    [GeneratedRegex(@"^\s*\d+:\s+d2i\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapD2iInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+d2f\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapD2fInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+i2b\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapI2bInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+f2d\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapF2dInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+isub\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapIsubInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+i2f\b", RegexOptions.CultureInvariant)]
    internal static partial Regex JavapI2fInsnRegex();

    [GeneratedRegex(@"^\s*\d+:\s+(ifeq|ifne|goto)\s+(\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionalBranchInsnRegex();

    /// <summary>
    /// <c>javap -c</c> wraps long comments so <c>PartDefinition.addOrReplaceChild</c> can be split across two physical lines.
    /// Fold those continuations into the previous line so mesh binding / pose / addBox detection sees whole tokens.
    /// </summary>
    internal static List<string> FoldJavapWrappedBytecodeLines(List<string> lines)
    {
        var folded = new List<string>();
        foreach (var line in lines)
        {
            var t = line.TrimEnd('\r');
            if (t.Length == 0)
            {
                continue;
            }

            if (folded.Count > 0 && ShouldFoldJavapContinuationIntoPreviousLine(t))
            {
                folded[^1] = folded[^1] + t.TrimStart();
            }
            else
            {
                folded.Add(t);
            }
        }

        return folded;
    }

    internal static bool ShouldFoldJavapContinuationIntoPreviousLine(string line)
    {
        if (JavapInsnIndexStartRegex().IsMatch(line))
        {
            return false;
        }

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("Code:", StringComparison.Ordinal) ||
            trimmed.StartsWith("LineNumberTable:", StringComparison.Ordinal) ||
            trimmed.StartsWith("StackMapTable:", StringComparison.Ordinal) ||
            trimmed.StartsWith("LocalVariableTable", StringComparison.Ordinal) ||
            trimmed.StartsWith("public ", StringComparison.Ordinal) ||
            trimmed.StartsWith("static ", StringComparison.Ordinal) ||
            trimmed.StartsWith("Compiled from", StringComparison.Ordinal) ||
            trimmed.Contains(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// <c>javap -c</c> often wraps long <c>// Method …</c> comments across lines; merge non-instruction continuation
    /// lines so overload signatures (e.g. <c>CubeDeformation;FF</c>) stay visible for parsing.
    /// </summary>
    internal static string MergeJavapCommentContinuation(List<string> seg, int invokeLineIdx)
    {
        var sb = new StringBuilder(seg[invokeLineIdx]);
        for (var k = invokeLineIdx + 1; k < seg.Count; k++)
        {
            var t = seg[k];
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            if (JavapInsnIndexStartRegex().IsMatch(t))
            {
                break;
            }

            sb.Append(t.Trim());
        }

        return sb.ToString();
    }

    internal static bool TryParseLineBytecodeOffset(string line, out int offset)
    {
        offset = 0;
        var m = Regex.Match(line.Trim(), @"^\s*(\d+):", RegexOptions.CultureInvariant);
        return m.Success &&
               int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset);
    }

    internal static bool TryParseConditionalBranch(string line, out string op, out int targetOffset)
    {
        op = "";
        targetOffset = 0;
        var m = ConditionalBranchInsnRegex().Match(line.Trim());
        if (!m.Success)
        {
            return false;
        }

        op = m.Groups[1].Value;
        return int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetOffset);
    }

    internal static bool IsJavapMethodCodeHeaderLine(string line) =>
        line.TrimStart().StartsWith("Code:", StringComparison.Ordinal);

    internal static bool IsJavapPopLine(string line) =>
        Regex.IsMatch(line.Trim(), @"^\d+:\s+pop\s*$", RegexOptions.CultureInvariant);

    internal static bool IsBackwardStackNoiseLine(string line)
    {
        var insn = line.TrimStart();
        return Regex.IsMatch(insn, @"^\d+:\s+(pop|nop|swap|dup)\b", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(insn, @"^\d+:\s+aload_", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(insn, @"^\d+:\s+aload\s+\d+", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(insn, @"^\d+:\s+checkcast\b", RegexOptions.CultureInvariant);
    }

    internal static bool IsDynamicDoublePlaceholderLoad(string line)
    {
        var t = line.TrimStart();
        return Regex.IsMatch(t, @"^\d+:\s+dload_", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(t, @"^\d+:\s+dload\s+\d+", RegexOptions.CultureInvariant);
    }

    internal static bool ObfJavapLineReferencesShortType(string line, string obfShort) =>
        line.Contains($"L{obfShort};", StringComparison.Ordinal) ||
        line.Contains($" {obfShort}.", StringComparison.Ordinal) ||
        line.Contains($"class {obfShort}", StringComparison.Ordinal);

    internal static bool TryParseAloadLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapAloadLocalSlotRegex(), out slot);

    internal static bool TryParseAstoreLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapAstoreLocalSlotRegex(), out slot);

    internal static bool TryParseFstoreLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapFstoreLocalSlotRegex(), out slot);

    internal static bool TryParseFloadLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapFloadLocalSlotRegex(), out slot);

    internal static bool TryParseIloadLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapIloadLocalSlotRegex(), out slot);

    internal static bool TryParseIstoreLocalSlot(string line, out int slot) =>
        TryParseLocalSlot(line, JavapIstoreLocalSlotRegex(), out slot);

    internal static bool TryFindLdcStringBeforeLine(IReadOnlyList<string> lines, int lineIdx, out string? value)
    {
        value = null;
        for (var j = lineIdx - 1; j >= 0 && j > lineIdx - 12; j--)
        {
            var m = LdcStringRegex().Match(lines[j]);
            if (!m.Success)
            {
                continue;
            }

            value = m.Groups[1].Value;
            return true;
        }

        return false;
    }

    internal static bool TryParseFloatLine(string line, out double v)
    {
        v = 0;
        var m = LdcFloatRegex().Match(line);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
        {
            return true;
        }

        if (line.Contains("fconst_0", StringComparison.Ordinal))
        {
            v = 0;
            return true;
        }

        if (line.Contains("fconst_1", StringComparison.Ordinal))
        {
            v = 1;
            return true;
        }

        if (line.Contains("fconst_2", StringComparison.Ordinal))
        {
            v = 2;
            return true;
        }

        return false;
    }

    internal static bool TryParseIntLine(string line, out double iv)
    {
        iv = 0;
        var m = LdcIntRegex().Match(line);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
        {
            return true;
        }

        m = BipushRegex().Match(line);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
        {
            return true;
        }

        m = SipushRegex().Match(line);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
        {
            return true;
        }

        if (IconstM1Regex().IsMatch(line))
        {
            iv = -1;
            return true;
        }

        if (line.Contains("iconst_0", StringComparison.Ordinal))
        {
            iv = 0;
            return true;
        }

        if (line.Contains("iconst_1", StringComparison.Ordinal))
        {
            iv = 1;
            return true;
        }

        if (line.Contains("iconst_2", StringComparison.Ordinal))
        {
            iv = 2;
            return true;
        }

        if (line.Contains("iconst_3", StringComparison.Ordinal))
        {
            iv = 3;
            return true;
        }

        if (line.Contains("iconst_4", StringComparison.Ordinal))
        {
            iv = 4;
            return true;
        }

        if (line.Contains("iconst_5", StringComparison.Ordinal))
        {
            iv = 5;
            return true;
        }

        return false;
    }

    internal static bool TryConsumeIntBackward(List<string> seg, ref int j, int minIdx, out int vi)
    {
        vi = 0;
        while (j >= minIdx)
        {
            if (string.IsNullOrWhiteSpace(seg[j]))
            {
                j--;
                continue;
            }

            if (IsBackwardStackNoiseLine(seg[j]))
            {
                j--;
                continue;
            }

            if (TryParseIntLine(seg[j], out var dv))
            {
                vi = (int)Math.Round(dv);
                j--;
                return true;
            }

            return false;
        }

        return false;
    }

    internal static bool TryConsumeFloatBackward(List<string> seg, ref int j, int minIdx, out double fv)
    {
        fv = 0;
        while (j >= minIdx)
        {
            if (string.IsNullOrWhiteSpace(seg[j]))
            {
                j--;
                continue;
            }

            if (IsBackwardStackNoiseLine(seg[j]))
            {
                j--;
                continue;
            }

            if (TryParseFloatLine(seg[j], out fv))
            {
                j--;
                return true;
            }

            return false;
        }

        return false;
    }

    internal static bool TryConsumeLdcStringBackward(List<string> seg, ref int j, int minIdx, out string? quadKey)
    {
        quadKey = null;
        while (j >= minIdx)
        {
            if (string.IsNullOrWhiteSpace(seg[j]))
            {
                j--;
                continue;
            }

            if (IsBackwardStackNoiseLine(seg[j]))
            {
                j--;
                continue;
            }

            var m = LdcStringRegex().Match(seg[j]);
            if (!m.Success)
            {
                return false;
            }

            quadKey = m.Groups[1].Value;
            j--;
            return true;
        }

        return false;
    }

    internal static bool TryParseIntOperandBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> intLocals, out int value)
    {
        value = 0;
        while (j >= minIdx && IsBackwardStackNoiseLine(lines[j]))
        {
            j--;
        }

        if (j < minIdx)
        {
            return false;
        }

        if (TryParseIntLine(lines[j], out var iv))
        {
            value = (int)Math.Round(iv);
            j--;
            return true;
        }

        if (TryParseIloadLocalSlot(lines[j], out var slot) && intLocals.TryGetValue(slot, out value))
        {
            j--;
            return true;
        }

        return false;
    }

    /// <summary>Parses <c>bipush</c>/<c>iload</c>, <c>isub</c>, <c>i2f</c> stacks used in poses and leg boxes.</summary>
    internal static bool TryParseIntToFloatOperandBackward(List<string> lines, ref int j, int minIdx,
        IReadOnlyDictionary<int, int> intLocals, out double fv)
    {
        fv = 0;
        var mark = j;
        if (j < minIdx || !MergeJavapCommentContinuation(lines, j).Contains("i2f", StringComparison.Ordinal))
        {
            return false;
        }

        j--;
        if (j >= minIdx && JavapIsubInsnRegex().IsMatch(lines[j].TrimStart()))
        {
            j--;
            if (!TryParseIntOperandBackward(lines, ref j, minIdx, intLocals, out var rhs) ||
                !TryParseIntOperandBackward(lines, ref j, minIdx, intLocals, out var lhs))
            {
                j = mark;
                return false;
            }

            fv = lhs - rhs;
            return true;
        }

        if (!TryParseIntOperandBackward(lines, ref j, minIdx, intLocals, out var iv))
        {
            j = mark;
            return false;
        }

        fv = iv;
        return true;
    }

    internal static Match MatchLdcString(string line) => LdcStringRegex().Match(line);

    internal static bool IsInsnIndexStartLine(string line) => JavapInsnIndexStartRegex().IsMatch(line);

    private static bool TryParseLocalSlot(string line, Regex regex, out int slot)
    {
        slot = 0;
        var m = regex.Match(line);
        if (!m.Success)
        {
            return false;
        }

        var g1 = m.Groups[1];
        var g2 = m.Groups[2];
        var s = g1.Success ? g1.Value : g2.Value;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot);
    }
}