using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Selects a specular ONNX model path from per-resolution mapping using <b>ceil</b> policy:
/// smallest configured resolution &gt;= texture size; if none, largest configured resolution.
/// Falls back to <see cref="AutoPbrOptions.MlSpecularModelPath"/> when the map is empty or has no valid entries.
/// </summary>
public static class MlSpecularModelResolution
{
    /// <summary>
    /// Builds a sanitized map: positive resolution keys, non-whitespace paths, last write wins on duplicate keys.
    /// </summary>
    public static IReadOnlyDictionary<int, string> SanitizeMap(
        IReadOnlyDictionary<int, string>? map)
    {
        if (map is null || map.Count == 0)
            return new Dictionary<int, string>();

        var d = new Dictionary<int, string>();
        foreach (var kv in map)
        {
            if (kv.Key <= 0)
                continue;
            var p = kv.Value?.Trim();
            if (string.IsNullOrEmpty(p))
                continue;
            d[kv.Key] = p;
        }

        return d;
    }

    /// <summary>
    /// Resolves the model path for <paramref name="textureSize"/> (typically square edge length after crop).
    /// </summary>
    /// <param name="selectedResolution">The resolution key chosen from the map, or null when using fallback path only.</param>
    public static bool TryResolveModelPath(
        AutoPbrOptions options,
        int textureSize,
        out string? modelPath,
        out int? selectedResolution,
        out string? diagnostic)
    {
        diagnostic = null;
        modelPath = null;
        selectedResolution = null;

        if (!options.UseMlSpecularPredictor)
        {
            diagnostic = "ML specular is disabled.";
            return false;
        }

        var sanitized = SanitizeMap(options.MlSpecularModelPathsByResolution);
        var fallback = string.IsNullOrWhiteSpace(options.MlSpecularModelPath)
            ? null
            : options.MlSpecularModelPath!.Trim();

        if (sanitized.Count == 0)
        {
            if (fallback is null)
            {
                diagnostic = "ML specular is enabled but no model path is configured (set default or per-resolution paths).";
                return false;
            }

            modelPath = fallback;
            return true;
        }

        var size = Math.Max(1, textureSize);
        var keys = sanitized.Keys.Where(k => k > 0).OrderBy(k => k).ToArray();
        if (keys.Length == 0)
        {
            if (fallback is null)
            {
                diagnostic = "ML specular: per-resolution map had no valid entries.";
                return false;
            }

            modelPath = fallback;
            return true;
        }

        // Ceil: smallest key >= size
        foreach (var k in keys)
        {
            if (k >= size)
            {
                modelPath = sanitized[k];
                selectedResolution = k;
                return true;
            }
        }

        // Larger than all configured: use largest
        var maxKey = keys[^1];
        modelPath = sanitized[maxKey];
        selectedResolution = maxKey;
        return true;
    }
}
