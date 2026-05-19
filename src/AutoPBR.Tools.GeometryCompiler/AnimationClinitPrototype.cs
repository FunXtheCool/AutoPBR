namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Prototype hook for <c>*Animation</c> <c>&lt;clinit&gt;</c> sidecars emitted as <c>javap -c</c> text
/// (see <c>minecraft-client-model-index-*-animation-init</c> in <c>docs/generated</c>).
/// </summary>
internal static class AnimationClinitPrototype
{
    /// <summary>Returns first meaningful line of a <c>.javapc.txt</c> sidecar for quick indexing.</summary>
    public static bool TryReadSummaryLine(string generatedRoot, string relativePath, out string summary)
    {
        summary = string.Empty;
        var path = Path.Combine(generatedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return false;
        }

        foreach (var line in File.ReadLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("Compiled from", StringComparison.Ordinal))
            {
                continue;
            }

            summary = t.Length > 200 ? t[..200] + "…" : t;
            return true;
        }

        return false;
    }
}
