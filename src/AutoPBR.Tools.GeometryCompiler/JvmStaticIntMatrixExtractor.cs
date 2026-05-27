using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Extracts <c>static final int[][]</c> tables from class <c>&lt;clinit&gt;</c> bytecode (worm segment sizes, UV origins, etc.).
/// </summary>
internal static class JvmStaticIntMatrixExtractor
{
    private static readonly Regex PutStaticFieldRegex = new(
        @"putstatic\s+#\d+\s+//\s*Field\s+(?:[\w$./]+\.)?(\w+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static IReadOnlyDictionary<string, int[][]> ExtractFromClass(ReadOnlySpan<byte> classFile)
    {
        if (!JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(classFile, "<clinit>", out var lines) ||
            lines.Count == 0)
        {
            return new Dictionary<string, int[][]>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, int[][]>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParseOuterArrayStart(lines, i, out var outerLen, out i))
            {
                continue;
            }

            var outer = new List<int[]>(outerLen);
            while (outer.Count < outerLen && TryParseInnerRow(lines, ref i, out var row))
            {
                outer.Add(row);
            }

            if (i < lines.Count && TryParsePutStaticField(lines[i], out var fieldName))
            {
                result[fieldName] = outer.ToArray();
            }
        }

        return result;
    }

    private static bool TryParseOuterArrayStart(List<string> lines, int index, out int outerLen, out int nextIndex)
    {
        outerLen = 0;
        nextIndex = index;
        if (!TryParseIntInsn(lines, index, out outerLen))
        {
            return false;
        }

        if (index + 1 >= lines.Count || !lines[index + 1].Contains("anewarray", StringComparison.Ordinal))
        {
            return false;
        }

        nextIndex = index + 2;
        return outerLen > 0;
    }

    private static bool TryParseInnerRow(List<string> lines, ref int index, out int[] row)
    {
        row = [];
        var i = index;
        if (i >= lines.Count || !lines[i].Contains("dup", StringComparison.Ordinal))
        {
            return false;
        }

        i++;
        if (!TryParseIntInsn(lines, i++, out _))
        {
            return false;
        }

        if (!TryParseIntInsn(lines, i++, out var rowLen) || rowLen <= 0)
        {
            return false;
        }

        if (i >= lines.Count || !lines[i].Contains("newarray", StringComparison.Ordinal) ||
            !lines[i].Contains("int", StringComparison.Ordinal))
        {
            return false;
        }

        i++;
        row = new int[rowLen];
        var filled = 0;
        while (i < lines.Count && filled < rowLen)
        {
            if (!lines[i].Contains("dup", StringComparison.Ordinal))
            {
                break;
            }

            i++;
            if (!TryParseIntInsn(lines, i++, out var cellIndex) || cellIndex < 0 || cellIndex >= rowLen)
            {
                return false;
            }

            if (!TryParseIntInsn(lines, i++, out var cellValue))
            {
                return false;
            }

            if (i >= lines.Count || !lines[i].Contains("iastore", StringComparison.Ordinal))
            {
                return false;
            }

            row[cellIndex] = cellValue;
            filled++;
            i++;
        }

        if (i >= lines.Count || !lines[i].Contains("aastore", StringComparison.Ordinal))
        {
            return false;
        }

        index = i + 1;
        return filled == rowLen;
    }

    private static bool TryParsePutStaticField(string line, out string fieldName)
    {
        fieldName = string.Empty;
        var m = PutStaticFieldRegex.Match(line);
        if (!m.Success)
        {
            return false;
        }

        fieldName = m.Groups[1].Value;
        return true;
    }

    private static bool TryParseIntInsn(List<string> lines, int index, out int value)
    {
        value = 0;
        if (index < 0 || index >= lines.Count)
        {
            return false;
        }

        var line = lines[index].Trim();
        var m = Regex.Match(line, @"^\s*\d+:\s+(?:bipush|sipush)\s+(-?\d+)", RegexOptions.CultureInvariant);
        if (m.Success)
        {
            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        if (line.Contains("iconst_m1", StringComparison.Ordinal))
        {
            value = -1;
            return true;
        }

        m = Regex.Match(line, @"^\s*\d+:\s+iconst_(\d+)", RegexOptions.CultureInvariant);
        if (m.Success)
        {
            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        m = Regex.Match(line, @"^\s*\d+:\s+ldc\s+#\d+\s+//\s*int\s+(-?\d+)", RegexOptions.CultureInvariant);
        return m.Success &&
               int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
