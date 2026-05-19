using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Infers Minecraft game version from resource-pack paths and <c>pack.mcmeta</c> when possible.
/// Used to pick 1.21.11 versus modern native preview profiles.
/// </summary>
internal static class MinecraftPreviewVersionDetection
{
    private static readonly Regex VersionToken =
        new(@"\b(\d{1,2})\.(\d{1,3})(?:\.(\d{1,3}))?\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Resource-pack format thresholds (ascending). The highest matched row estimates the pack's target game version.
    /// Sourced from Minecraft Wiki pack format history (resource packs).
    /// </summary>
    private static readonly (double Format, Version Game)[] ResourcePackFormatCeilings =
    [
        (34, new Version(1, 21, 1)),
        (42, new Version(1, 21, 3)),
        (46, new Version(1, 21, 4)),
        (55, new Version(1, 21, 5)),
        (63, new Version(1, 21, 6)),
        (64, new Version(1, 21, 8)),
        (69, new Version(1, 21, 10)),
        (75, new Version(1, 21, 11)),
        (84, new Version(26, 1, 0)),
    ];

    public static bool TryDetect(string? inputZipPath, string? extractedPackDir, out Version gameVersion)
    {
        gameVersion = null!;
        Version? fromPaths = null;
        foreach (var path in new[] { inputZipPath, extractedPackDir })
        {
            if (TryDetectFromPath(path, out var v))
            {
                fromPaths = fromPaths is null || v > fromPaths ? v : fromPaths;
            }
        }

        Version? fromMcmeta = null;
        if (TryReadPackTargetVersion(extractedPackDir, out var mcmetaVer))
        {
            fromMcmeta = mcmetaVer;
        }
        else if (TryReadPackTargetVersionFromZip(inputZipPath, out mcmetaVer))
        {
            fromMcmeta = mcmetaVer;
        }

        if (fromPaths is null && fromMcmeta is null)
        {
            return false;
        }

        if (fromPaths is not null && fromMcmeta is not null)
        {
            gameVersion = fromPaths > fromMcmeta ? fromPaths : fromMcmeta;
            return true;
        }

        gameVersion = fromPaths ?? fromMcmeta!;
        return true;
    }

    public static bool TryDetectFromPath(string? path, out Version gameVersion)
    {
        gameVersion = null!;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        Version? best = null;
        foreach (Match m in VersionToken.Matches(normalized))
        {
            if (!TryParseVersionMatch(m, out var v) || !IsPlausibleGameVersion(v))
            {
                continue;
            }

            best = best is null || v > best ? v : best;
        }

        if (best is null)
        {
            return false;
        }

        gameVersion = best;
        return true;
    }

    private static bool IsPlausibleGameVersion(Version v) =>
        v.Major is 1 or >= 20;

    private static bool TryParseVersionMatch(Match m, out Version version)
    {
        version = null!;
        if (!int.TryParse(m.Groups[1].Value, out var major) ||
            !int.TryParse(m.Groups[2].Value, out var minor))
        {
            return false;
        }

        var build = 0;
        if (m.Groups[3].Success && !int.TryParse(m.Groups[3].Value, out build))
        {
            return false;
        }

        version = new Version(major, minor, build);
        return true;
    }

    private static bool TryReadPackTargetVersion(string? extractedPackDir, out Version gameVersion)
    {
        gameVersion = null!;
        if (string.IsNullOrWhiteSpace(extractedPackDir))
        {
            return false;
        }

        var mcmetaPath = Path.Combine(extractedPackDir, "pack.mcmeta");
        if (!File.Exists(mcmetaPath))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(mcmetaPath));
            return TryParsePackSection(doc.RootElement, out gameVersion);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadPackTargetVersionFromZip(string? zipPath, out Version gameVersion)
    {
        gameVersion = null!;
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return false;
        }

        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = zip.GetEntry("pack.mcmeta") ?? zip.GetEntry("pack.mcmeta".Replace('/', '\\'));
            if (entry is null)
            {
                return false;
            }

            using var stream = entry.Open();
            using var doc = JsonDocument.Parse(stream);
            return TryParsePackSection(doc.RootElement, out gameVersion);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParsePackSection(JsonElement root, out Version gameVersion)
    {
        gameVersion = null!;
        if (!root.TryGetProperty("pack", out var pack))
        {
            return false;
        }

        if (pack.TryGetProperty("pack_format", out var pf) && pf.ValueKind == JsonValueKind.Number)
        {
            return TryMapResourcePackFormat(pf.GetDouble(), out gameVersion);
        }

        double? format = null;
        if (pack.TryGetProperty("max_format", out var maxF) && maxF.ValueKind == JsonValueKind.Number)
        {
            format = maxF.GetDouble();
        }
        else if (pack.TryGetProperty("min_format", out var minF) && minF.ValueKind == JsonValueKind.Number)
        {
            format = minF.GetDouble();
        }

        return format is not null && TryMapResourcePackFormat(format.Value, out gameVersion);
    }

    private static bool TryMapResourcePackFormat(double packFormat, out Version gameVersion)
    {
        gameVersion = new Version(1, 0);
        var matched = false;
        foreach (var (threshold, game) in ResourcePackFormatCeilings)
        {
            if (packFormat + 1e-6 < threshold)
            {
                continue;
            }

            gameVersion = game;
            matched = true;
        }

        return matched;
    }
}
