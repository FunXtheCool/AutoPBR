using System.Diagnostics;

namespace AutoPBR.Tools.GeometryCompiler;

internal static class GeometryJavapLocator
{
    public static string? FindJavap()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JAVA_HOME")))
        {
            var home = Environment.GetEnvironmentVariable("JAVA_HOME")!;
            foreach (var rel in new[] { "bin/javap.exe", "bin/javap", "bin\\javap.exe", "bin\\javap" })
            {
                var p = Path.Combine(home, rel);
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        try
        {
            var fileName = OperatingSystem.IsWindows() ? "where" : "which";
            using var which = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                ArgumentList = { "javap" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (which is null)
            {
                return null;
            }

            var line = which.StandardOutput.ReadLine();
            which.WaitForExit();
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }
        catch
        {
            return null;
        }
    }
}
