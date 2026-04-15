namespace AutoPBR.Core;

/// <summary>
/// Canonical parsing for foliage mode values shared by App/Core/CLI.
/// Accepts both UI labels and token forms (e.g. "no-height").
/// </summary>
internal static class FoliageModeResolver
{
    public static bool IsIgnoreAll(string? mode) =>
        TryNormalize(mode, out var normalized) &&
        normalized.Equals("Ignore All", StringComparison.Ordinal);

    public static bool IsNoHeight(string? mode) =>
        TryNormalize(mode, out var normalized) &&
        normalized.Equals("No Height", StringComparison.Ordinal);

    private static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = "Convert All";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var t = raw.Trim();
        if (t.Equals("Ignore All", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("ignore-all", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("ignore_all", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("ignoreall", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("ignore plants", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Ignore All";
            return true;
        }

        if (t.Equals("No Height", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("no-height", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("no_height", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("noheight", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "No Height";
            return true;
        }

        if (t.Equals("Convert All", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("convert-all", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("convert_all", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("convertall", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Convert All";
            return true;
        }

        return false;
    }
}
