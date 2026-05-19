using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Extracts <c>static final float[]</c> tables from class <c>&lt;clinit&gt;</c> (guardian spike tables, etc.).</summary>
internal static class JvmStaticFloatArrayExtractor
{
    private static readonly Regex PutStaticFieldRegex = new(
        @"putstatic\s+#\d+\s+//\s*Field\s+[\w$./]+\.(\w+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static IReadOnlyDictionary<string, float[]> ExtractFromClass(ReadOnlySpan<byte> classFile)
    {
        if (!JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(classFile, "<clinit>", out var lines) ||
            lines.Count == 0)
        {
            return new Dictionary<string, float[]>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, float[]>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParseFloatArrayStart(lines, i, out var len, out i))
            {
                continue;
            }

            var values = new float[len];
            var filled = 0;
            while (i < lines.Count && filled < len)
            {
                if (!lines[i].Contains("dup", StringComparison.Ordinal))
                {
                    break;
                }

                i++;
                if (!TryParseIntInsn(lines, i++, out var cellIndex) || cellIndex < 0 || cellIndex >= len)
                {
                    break;
                }

                if (!TryParseFloatInsn(lines, i++, out var cellValue))
                {
                    break;
                }

                if (i >= lines.Count || !lines[i].Contains("fastore", StringComparison.Ordinal))
                {
                    break;
                }

                values[cellIndex] = cellValue;
                filled++;
                i++;
            }

            if (i < lines.Count && TryParsePutStaticField(lines[i], out var fieldName))
            {
                result[fieldName] = values;
            }
        }

        return result;
    }

    private static bool TryParseFloatArrayStart(List<string> lines, int index, out int len, out int nextIndex)
    {
        len = 0;
        nextIndex = index;
        if (!TryParseIntInsn(lines, index, out len))
        {
            return false;
        }

        if (index + 1 >= lines.Count || !lines[index + 1].Contains("newarray", StringComparison.Ordinal) ||
            !lines[index + 1].Contains("float", StringComparison.Ordinal))
        {
            return false;
        }

        nextIndex = index + 2;
        return len > 0;
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

    private static bool TryParseFloatInsn(List<string> lines, int index, out float value)
    {
        value = 0;
        if (index < 0 || index >= lines.Count)
        {
            return false;
        }

        var line = lines[index].Trim();
        var m = Regex.Match(line, @"^\s*\d+:\s+ldc\s+#\d+\s+//\s*float\s+(-?[\d.]+)f", RegexOptions.CultureInvariant);
        if (m.Success)
        {
            return float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        if (line.Contains("fconst_0", StringComparison.Ordinal))
        {
            value = 0;
            return true;
        }

        if (line.Contains("fconst_1", StringComparison.Ordinal))
        {
            value = 1;
            return true;
        }

        if (line.Contains("fconst_2", StringComparison.Ordinal))
        {
            value = 2;
            return true;
        }

        return false;
    }
}
