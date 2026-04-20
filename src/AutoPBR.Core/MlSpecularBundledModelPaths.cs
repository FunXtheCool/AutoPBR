using System.Globalization;

namespace AutoPBR.Core;

/// <summary>
/// Conventional layout for bundled per-resolution specular ONNX models.
/// Primary layout is <c>Data/ONNX-AI/SpecLab/SpecLab_{res}x.onnx</c>.
/// Legacy <c>Data/models/&lt;res&gt;/</c> and old <c>specular_predictor*</c> filenames are retained as compatibility fallbacks.
/// </summary>
public static class MlSpecularBundledModelPaths
{
    /// <summary>Standard training/inference resolutions supported by the UI (up to 256).</summary>
    public static readonly int[] StandardResolutions = [16, 32, 64, 128, 256];

    /// <summary>
    /// Returns an absolute path to a bundled model for <paramref name="resolution"/> if a known filename exists.
    /// </summary>
    public static string? TryResolveExistingBundledPath(int resolution, string baseDirectory)
    {
        if (resolution <= 0 || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Data", "ONNX-AI", "SpecLab", $"SpecLab_{resolution}x.onnx"),
            Path.Combine(baseDirectory, "Data", "ONNX-AI", "SpecLab", $"SpecLab_{resolution}.onnx"),
            Path.Combine(baseDirectory, "Data", "ONNX-AI", "SpecLab", $"specular_predictor_{resolution}x.onnx"),
            Path.Combine(baseDirectory, "Data", "ONNX-AI", "SpecLab", $"specular_predictor_{resolution}.onnx"),
            Path.Combine(baseDirectory, "Data", "models", resolution.ToString(CultureInfo.InvariantCulture), $"SpecLab_{resolution}x.onnx"),
            Path.Combine(baseDirectory, "Data", "models", resolution.ToString(CultureInfo.InvariantCulture), $"SpecLab_{resolution}.onnx"),
            Path.Combine(baseDirectory, "Data", "models", resolution.ToString(CultureInfo.InvariantCulture), $"specular_predictor_{resolution}x.onnx"),
            Path.Combine(baseDirectory, "Data", "models", resolution.ToString(CultureInfo.InvariantCulture), $"specular_predictor_{resolution}.onnx"),
            $"specular_predictor_{resolution}x.onnx",
            $"specular_predictor_{resolution}.onnx",
            $"SpecLab_{resolution}x.onnx",
            $"SpecLab_{resolution}.onnx"
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a map for every standard resolution that has a bundled file on disk.
    /// </summary>
    public static IReadOnlyDictionary<int, string>? TryBuildMapFromBundledFiles(string baseDirectory)
    {
        var d = new Dictionary<int, string>();
        foreach (var res in StandardResolutions)
        {
            var p = TryResolveExistingBundledPath(res, baseDirectory);
            if (p is not null)
            {
                d[res] = p;
            }
        }

        return d.Count > 0 ? d : null;
    }
}
