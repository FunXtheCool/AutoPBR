namespace AutoPBR.App.Services;

/// <summary>Persists in-memory log lines to timestamped files under the app logs directory, with rotation (keep at most 10 files).</summary>
internal static class LogService
{
    private const int MaxLogFiles = 10;

    public static string LogsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR", "logs");

    /// <summary>Write lines to a new timestamped log file and remove older files beyond <see cref="MaxLogFiles"/>.</summary>
    public static void SaveToFile(IEnumerable<string> lines)
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"AutoPBR_{timestamp}.log";
            var fullPath = Path.Combine(LogsDirectory, fileName);
            File.WriteAllLines(fullPath, lines);

            var files = Directory.GetFiles(LogsDirectory, "AutoPBR_*.log")
                .OrderBy(File.GetCreationTimeUtc)
                .ToList();
            while (files.Count > MaxLogFiles)
            {
                var oldest = files[0];
                files.RemoveAt(0);
                try
                {
                    File.Delete(oldest);
                }
                catch
                {
                    /* ignore cleanup errors */
                }
            }
        }
        catch
        {
            // Logging should never crash the app; ignore IO errors.
        }
    }
}
