namespace AutoPBR.Core;

/// <summary>
/// Conventional layout for bundled per-resolution specular ONNX models under <c>Data/models/&lt;res&gt;/</c>.
/// </summary>
public static class MlSpecularBundledModelPaths
{
    /// <summary>Standard training/inference resolutions supported by the UI (up to 256).</summary>
    public static readonly int[] StandardResolutions = [16, 32, 64, 128, 256];

    /// <summary>
    /// Returns an absolute path to a bundled model for <paramref name="resolution"/> if a known filename exists under
    /// <c>Data/models/&lt;resolution&gt;/</c>, otherwise null.
    /// </summary>
    public static string? TryResolveExistingBundledPath(int resolution, string baseDirectory)
    {
        if (resolution <= 0 || string.IsNullOrWhiteSpace(baseDirectory))
            return null;

        var dir = Path.Combine(baseDirectory, "Data", "models", resolution.ToString());
        var candidates = new[]
        {
            $"specular_predictor_{resolution}x.onnx",
            $"specular_predictor_{resolution}.onnx"
        };
        foreach (var name in candidates)
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    /// <summary>
    /// Builds a map for every standard resolution that has a file on disk under <c>Data/models/&lt;res&gt;/</c>.
    /// </summary>
    public static IReadOnlyDictionary<int, string>? TryBuildMapFromBundledFiles(string baseDirectory)
    {
        var d = new Dictionary<int, string>();
        foreach (var res in StandardResolutions)
        {
            var p = TryResolveExistingBundledPath(res, baseDirectory);
            if (p is not null)
                d[res] = p;
        }

        return d.Count > 0 ? d : null;
    }
}
