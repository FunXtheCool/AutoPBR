using System.Collections.Concurrent;
using System.Diagnostics;

namespace AutoPBR.Tools.AnimationCompiler;

internal static class JavapRunner
{
    private static readonly ConcurrentDictionary<string, Lazy<CachedDisasm>> DisasmCache = new(StringComparer.Ordinal);

    private sealed record CachedDisasm(bool Ok, string Stdout, string? Error);

    internal static void ClearDisassemblyCacheForTests() => DisasmCache.Clear();

    public static bool TryDisassemble(string? javapExe, string clientJar, string javapClassArg, out string stdout,
        out string? error)
    {
        var key = BuildDisasmCacheKey(javapExe, clientJar, javapClassArg);
        if (DisasmCache.TryGetValue(key, out var existingLazy))
        {
            AnimationCompilerStats.NoteDisasmCacheHit();
            return UnpackCached(existingLazy.Value, out stdout, out error);
        }

        var lazy = DisasmCache.GetOrAdd(key, _ => new Lazy<CachedDisasm>(
            () => RunDisassembleUncached(javapExe, clientJar, javapClassArg),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return UnpackCached(lazy.Value, out stdout, out error);
    }

    private static bool UnpackCached(CachedDisasm c, out string stdout, out string? error)
    {
        stdout = c.Stdout;
        error = c.Error;
        return c.Ok;
    }

    private static string BuildDisasmCacheKey(string? javapExe, string clientJar, string javapClassArg)
    {
        string normExe;
        try
        {
            normExe = string.IsNullOrWhiteSpace(javapExe) ? "" : Path.GetFullPath(javapExe);
        }
        catch
        {
            normExe = javapExe ?? "";
        }

        string normJar;
        try
        {
            normJar = Path.GetFullPath(clientJar);
        }
        catch
        {
            normJar = clientJar;
        }

        return normExe + '\u241E' + normJar + '\u241E' + javapClassArg;
    }

    private static CachedDisasm RunDisassembleUncached(string? javapExe, string clientJar, string javapClassArg)
    {
        AnimationCompilerStats.NoteJavapSubprocess();
        if (string.IsNullOrWhiteSpace(javapExe))
        {
            return new CachedDisasm(false, string.Empty, "javap executable not found (set JAVA_HOME or PATH).");
        }

        if (!File.Exists(clientJar))
        {
            return new CachedDisasm(false, string.Empty, $"client.jar not found: {clientJar}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = javapExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-encoding");
        psi.ArgumentList.Add("UTF8");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("-classpath");
        psi.ArgumentList.Add(clientJar);
        psi.ArgumentList.Add(javapClassArg);

        using var p = Process.Start(psi);
        if (p is null)
        {
            return new CachedDisasm(false, string.Empty, "Failed to start javap.");
        }

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            return new CachedDisasm(false, stdout, $"javap exit {p.ExitCode}: {stderr}{stdout}");
        }

        return new CachedDisasm(true, stdout, null);
    }
}
