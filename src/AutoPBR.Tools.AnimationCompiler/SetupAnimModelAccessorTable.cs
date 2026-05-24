using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>Parses zero-arg <c>protected float get*()</c> helpers that return a single ldc constant.</summary>
internal static class SetupAnimModelAccessorTable
{
    private static readonly Regex AccessorMethodRegex = new(
        @"protected\s+float\s+(\w+)\s*\(\s*\)\s*;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex LdcFloatReturnRegex = new(
        @"ldc\s+#\d+\s+//\s*float\s+(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)f\s+freturn",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static Dictionary<string, float> Parse(string javapStdout)
    {
        var map = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (Match m in AccessorMethodRegex.Matches(javapStdout))
        {
            var name = m.Groups[1].Value;
            var codeMark = javapStdout.IndexOf("    Code:", m.Index, StringComparison.Ordinal);
            if (codeMark < 0)
            {
                continue;
            }

            var end = javapStdout.IndexOf("\n    }", codeMark, StringComparison.Ordinal);
            var block = end < 0 ? javapStdout[codeMark..] : javapStdout[codeMark..end];
            var ldc = LdcFloatReturnRegex.Match(block);
            if (!ldc.Success)
            {
                continue;
            }

            if (float.TryParse(ldc.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                map[name] = f;
            }
        }

        return map;
    }
}
