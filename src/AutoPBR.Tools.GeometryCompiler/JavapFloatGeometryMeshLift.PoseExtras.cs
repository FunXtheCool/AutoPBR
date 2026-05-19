using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private static readonly Dictionary<string, double> KnownStaticFloatFields = new(StringComparer.Ordinal)
    {
        ["net/minecraft/util/Mth.PI"] = Math.PI,
        ["net/minecraft/util/Mth.TWO_PI"] = Math.PI * 2,
        ["java/lang/Math.PI"] = Math.PI
    };

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(\w+)\.(\w+):\(([^)]*)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ObfuscatedInvokeStaticMethodRegex();

    [GeneratedRegex(
        @"getstatic\s+#\d+\s+//\s*Field\s+([\w\./$]+)\.(\w+):F",
        RegexOptions.CultureInvariant)]
    private static partial Regex GetStaticFloatFieldRegex();

    private static bool SegmentContainsLoopBasedPoseMath(List<string> seg, int from, int to)
    {
        var hasGoto = false;
        var hasMath = false;
        for (var i = from; i <= to && i < seg.Count; i++)
        {
            var t = seg[i];
            if (t.Contains("goto", StringComparison.Ordinal))
            {
                hasGoto = true;
            }

            if (t.Contains("java/lang/Math.", StringComparison.Ordinal))
            {
                hasMath = true;
            }
        }

        return hasGoto && hasMath;
    }

    private static bool TryParseKnownStaticFloatField(string line, ICollection<string> poseWarnings, out double v)
    {
        v = 0;
        var m = GetStaticFloatFieldRegex().Match(line);
        if (!m.Success)
        {
            return false;
        }

        var key = m.Groups[1].Value + "." + m.Groups[2].Value;
        if (KnownStaticFloatFields.TryGetValue(key, out v))
        {
            return true;
        }

        poseWarnings.Add("static_field_inferred");
        return false;
    }

    private static bool TryAnnotateObfuscatedPartPoseFactory(string line, string poseShort,
        ICollection<string> poseWarnings)
    {
        if (_maps is null)
        {
            return false;
        }

        var m = ObfuscatedInvokeStaticMethodRegex().Match(line);
        if (!m.Success || !string.Equals(m.Groups[1].Value, poseShort, StringComparison.Ordinal))
        {
            return false;
        }

        if (_maps.TryResolveObfuscatedPartPoseFactory(poseShort, m.Groups[2].Value, m.Groups[3].Value,
                out _))
        {
            poseWarnings.Add("obf_factory_inferred");
            return true;
        }

        return false;
    }
}
