using System.Diagnostics;

namespace AutoPBR.Core.Preview;

public static partial class GeometryJavapPoseOracle
{
    /// <summary>Runs <c>javap -c -constants</c> against <paramref name="clientJar"/> (cached per process lifetime is caller-owned).</summary>
    internal static class GeometryJavapDisassembly
    {
        public static bool TryDisassemble(
            string? javapExecutable,
            string? clientJar,
            string officialJvmName,
            out string stdout,
            out string? error)
        {
            stdout = "";
            error = null;
            if (string.IsNullOrEmpty(clientJar) || string.IsNullOrEmpty(javapExecutable))
            {
                error = "client.jar or javap not configured";
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javapExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-classpath");
                psi.ArgumentList.Add(clientJar);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("-constants");
                psi.ArgumentList.Add(officialJvmName);

                using var p = Process.Start(psi);
                if (p is null)
                {
                    error = "failed to start javap";
                    return false;
                }

                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                stdout = stdoutTask.GetAwaiter().GetResult();
                var stderr = stderrTask.GetAwaiter().GetResult();
                if (p.ExitCode != 0)
                {
                    error = $"javap exit {p.ExitCode}: {stderr}";
                    stdout = "";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    private static string? FindJavapExecutable()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            foreach (var rel in new[] { "bin/javap.exe", "bin/javap", "bin\\javap.exe", "bin\\javap" })
            {
                var p = Path.Combine(javaHome, rel);
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

