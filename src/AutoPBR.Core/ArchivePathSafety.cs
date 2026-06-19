namespace AutoPBR.Core;

internal static class ArchivePathSafety
{
    public static bool TryResolveExtractionPath(string extractionRoot, string archivePath, out string destinationPath)
    {
        destinationPath = string.Empty;
        if (string.IsNullOrWhiteSpace(extractionRoot) || string.IsNullOrWhiteSpace(archivePath))
        {
            return false;
        }

        if (archivePath.Contains('\0') || Path.IsPathRooted(archivePath))
        {
            return false;
        }

        var normalizedArchivePath = archivePath.Replace('\\', '/');
        if (normalizedArchivePath.Length > 0 && normalizedArchivePath[0] == '/')
        {
            return false;
        }

        var segments = normalizedArchivePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(static s => s is "." or ".." || Path.IsPathRooted(s)))
        {
            return false;
        }

        var root = Path.GetFullPath(extractionRoot);
        var combined = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combined, root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        destinationPath = combined;
        return true;
    }
}
