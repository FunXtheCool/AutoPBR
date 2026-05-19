using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Runs <c>javap -c</c> and collects <c>// float …</c> constants from <c>createBodyLayer</c> plus static mesh factories
/// (named <c>MeshDefinition</c> / <c>LayerDefinition</c> helpers or ProGuard-mapped <c>createBodyLayer</c> / <c>createBase*</c> obfuscated names).
/// </summary>
internal static partial class JavapMethodFloatProbe
{
    [GeneratedRegex(@"//\s*float\s+([\d.+-eEfF]+)", RegexOptions.CultureInvariant)]
    private static partial Regex FloatCommentRegex();

    public static bool TryRun(string? javapExe, string clientJar, string javapClassArg, string methodName,
        out IReadOnlyList<float> floats, out string? error) =>
        TryRun(javapExe, clientJar, javapClassArg, methodName, null, null, javapClassArg, null, null, out floats,
            out error);

    public static bool TryRun(string? javapExe, string clientJar, string javapClassArg, string methodName,
        string? meshHostOfficialOuter, MojangMappingsParser? maps, string meshHostJavapArg,
        out IReadOnlyList<float> floats, out string? error) =>
        TryRun(javapExe, clientJar, javapClassArg, methodName, meshHostOfficialOuter, maps, meshHostJavapArg, null,
            null, out floats, out error);

    /// <param name="javapExe">Path to <c>javap</c> (optional; resolved when null).</param>
    /// <param name="clientJar">Minecraft client JAR path.</param>
    /// <param name="javapClassArg">Class argument for the root <c>javap -c</c> disassembly.</param>
    /// <param name="methodName">Method to scan for float comments (e.g. <c>createBodyLayer</c>).</param>
    /// <param name="meshHostOfficialOuter">Official JVM name of the mesh host class (optional).</param>
    /// <param name="maps">ProGuard / Mojang mappings (optional).</param>
    /// <param name="meshHostJavapArg"><c>javap</c> class argument for the mesh host.</param>
    /// <param name="precomputedMeshConcat">When non-empty, skips <c>javap</c> and <see cref="JavapClassDisassembly.ConcatMeshFactoryCodeDeep"/> (reuse from mesh resolution).</param>
    /// <param name="rootJavapStdoutForFallback">When <paramref name="precomputedMeshConcat"/> is empty, optional stdout to avoid a redundant disassemble of <paramref name="javapClassArg"/>.</param>
    /// <param name="floats">Collected float literals on success.</param>
    /// <param name="error">Failure message when returning false.</param>
    public static bool TryRun(string? javapExe, string clientJar, string javapClassArg, string methodName,
        string? meshHostOfficialOuter, MojangMappingsParser? maps, string meshHostJavapArg,
        string? precomputedMeshConcat, string? rootJavapStdoutForFallback,
        out IReadOnlyList<float> floats, out string? error)
    {
        floats = [];
        error = null;
        if (!string.IsNullOrEmpty(precomputedMeshConcat))
        {
            floats = CollectFloats(precomputedMeshConcat);
            return true;
        }

        string stdout;
        if (!string.IsNullOrEmpty(rootJavapStdoutForFallback))
        {
            stdout = rootJavapStdoutForFallback;
        }
        else if (!JavapClassDisassembly.TryDisassemble(javapExe, clientJar, javapClassArg, out stdout, out error))
        {
            return false;
        }

        var meshBlocks = JavapClassDisassembly.ConcatMeshFactoryCodeDeep(javapExe, clientJar, stdout,
            meshHostOfficialOuter, maps, meshHostJavapArg);
        if (meshBlocks.Length == 0)
        {
            string? block = null;
            if (maps is not null && !string.IsNullOrEmpty(meshHostOfficialOuter))
            {
                block = JavapClassDisassembly.ExtractFirstMappedMeshFactoryCode(stdout, maps, meshHostOfficialOuter);
            }

            block ??= JavapClassDisassembly.ExtractMethodCodeBlock(stdout, methodName);
            if (block is null)
            {
                error = $"Method '{methodName}' Code block not found in javap output.";
                return false;
            }

            meshBlocks = block;
        }

        floats = CollectFloats(meshBlocks);
        return true;
    }

    internal static List<float> CollectFloats(string javapCode)
    {
        var list = new List<float>();
        foreach (Match m in FloatCommentRegex().Matches(javapCode))
        {
            var s = m.Groups[1].Value.TrimEnd('f', 'F');
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                list.Add(f);
            }
        }

        return list;
    }
}
