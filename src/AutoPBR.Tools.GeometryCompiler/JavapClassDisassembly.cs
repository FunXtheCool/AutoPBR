using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;


namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Runs <c>javap -c</c> for a single class and exposes helpers to slice method bodies from stdout.
/// </summary>
internal static partial class JavapClassDisassembly
{
    private static readonly ConcurrentDictionary<string, Lazy<CachedDisasm>> DisasmCache = new(StringComparer.Ordinal);

    private sealed record CachedDisasm(bool Ok, string Stdout, string? Error);

    internal static void ClearDisassemblyCacheForTests() => DisasmCache.Clear();

    /// <summary>
    /// Synthetic line inserted between bytecode chunks merged from different classes (e.g. host <c>createBodyLayer</c> +
    /// <c>PlayerModel.createMesh</c> + <c>AbstractPiglinModel.addHead</c>) and between multiple static mesh <c>Code:</c> blocks from the
    /// same <c>javap</c> class (see <see cref="ConcatMeshFactoryCodeNamed"/> / <see cref="ConcatMeshFactoryCode"/>). Without it,
    /// <see cref="JavapFloatGeometryMeshLift"/> would reuse local-slot maps across unrelated methods and mis-nest parts.
    /// </summary>
    internal const string GeometryMeshIslandBoundaryMarker = "// __AUTOPBR_GEOMETRY_MESH_ISLAND__";

    private static readonly Regex StaticMeshDefinitionMethodDeclRegex = new(
        @"^\s+.*\bstatic\b.*\bMeshDefinition\s+(\w+)\s*\([^)]*\)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    /// <summary>Static factories returning <c>LayerDefinition</c> (cape/ears layers, effect models, etc.).</summary>
    private static readonly Regex StaticLayerDefinitionMethodDeclRegex = new(
        @"^\s+.*\bstatic\b.*\bLayerDefinition\s+(\w+)\s*\([^)]*\)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticReturnsMeshDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L[\w/$]+MeshDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticSameClassMeshDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w$]+):\([^)]*\)L[\w/$]+MeshDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticReturnsLayerDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L[\w/$]+LayerDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticObfuscatedReturnCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L(\w+);\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    public static bool TryDisassemble(string? javapExe, string clientJar, string javapClassArg, out string stdout,
        out string? error)
    {
        var key = BuildDisasmCacheKey(javapExe, clientJar, javapClassArg);
        if (DisasmCache.TryGetValue(key, out var existingLazy))
        {
            GeometryCompilerStats.NoteDisasmCacheHit();
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

    private static string ResolveJavapExecutable(string? javapExe) =>
        string.IsNullOrWhiteSpace(javapExe) ? GeometryJavapLocator.FindJavap() ?? "javap" : javapExe;

    private static CachedDisasm RunDisassembleUncached(string? javapExe, string clientJar, string javapClassArg)
    {
        GeometryCompilerStats.NoteJavapSubprocess();
        javapExe = ResolveJavapExecutable(javapExe);
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
