using System.Diagnostics;
using System.Text;

namespace AutoPBR.Cli;

/// <summary>
/// Spawns the <c>AutoPBR.Training.Ort.Launcher</c> executable so ORT Training 1.19 native binaries are not loaded in-process
/// alongside <c>AutoPBR.Core</c> ORT GPU 1.24.
/// </summary>
internal static class OrtSpecularTrainingCliDispatch
{
    public static int Run(ReadOnlySpan<string> trainingArgs)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(exeDir))
        {
            exeDir = Directory.GetCurrentDirectory();
        }

        var launcher = Path.Combine(exeDir, "AutoPBR.Training.Ort.Launcher.exe");
        if (!File.Exists(launcher))
        {
            Console.Error.WriteLine(
                $"Could not find '{launcher}'. Build the solution so the ORT training launcher is copied next to AutoPBR.Cli.exe.");
            return 1;
        }

        var sb = new StringBuilder();
        foreach (var s in trainingArgs)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(QuoteArg(s));
        }

        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = launcher,
            Arguments = sb.ToString(),
            UseShellExecute = false,
        });

        if (p is null)
        {
            return 1;
        }

        p.WaitForExit();
        return p.ExitCode;
    }

    private static string QuoteArg(string s)
    {
        if (s.Length == 0 || s.Any(static c => char.IsWhiteSpace(c) || c == '"'))
        {
            return '"' + s.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
        }

        return s;
    }
}
