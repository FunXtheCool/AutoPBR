using System.Diagnostics.CodeAnalysis;

namespace AutoPBR.Core;

/// <summary>Parses CLI entries like <c>16=C:\path\model.onnx</c> for <c>--ml-spec-model-map</c>.</summary>
public static class MlSpecularModelMapParsing
{
    /// <summary>Expected form: <c>&lt;positive-int&gt;=&lt;path&gt;</c> (path may contain spaces if quoted by the shell).</summary>
    public static bool TryParseMapEntry(string entry, out int resolution, [NotNullWhen(true)] out string? path,
        out string? error)
    {
        resolution = 0;
        path = null;
        error = null;
        if (string.IsNullOrWhiteSpace(entry))
        {
            error = "Entry is empty.";
            return false;
        }

        var idx = entry.IndexOf('=');
        if (idx <= 0)
        {
            error = "Expected res=path (missing '=').";
            return false;
        }

        var resSpan = entry.AsSpan(0, idx).Trim();
        if (!int.TryParse(resSpan, out resolution) || resolution <= 0)
        {
            error = $"Invalid resolution '{resSpan.ToString()}'.";
            return false;
        }

        path = entry[(idx + 1)..].Trim();
        if (string.IsNullOrEmpty(path))
        {
            error = "Path after '=' is empty.";
            return false;
        }

        return true;
    }
}
