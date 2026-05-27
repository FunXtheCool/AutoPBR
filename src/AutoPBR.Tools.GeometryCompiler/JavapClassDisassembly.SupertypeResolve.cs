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
    private static readonly Regex JavaClassExtendsRegex = new(
        @"public\s+(?:abstract\s+|final\s+)?class\s+[\w$.]+\s+extends\s+([\w$.]+)\b",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// <c>javap</c> sometimes prints <c>invokestatic … // Method foo:(…)</c> without a qualifying owner for static helpers
    /// declared on a superclass or abstract companion. Resolve by <c>Abstract*</c> naming heuristics and by walking the
    /// declared <c>extends</c> chain on the mesh host (humanoid, quadruped, aquatic, etc.).
    /// </summary>
    private static string? TryExtractNullOwnerStaticMeshFromHostSupertypes(string? javapExe, string clientJar,
        MojangMappingsParser? maps, string? meshHostOfficialOuter, string meshHostClassJavapStdout, string signatureNeedle)
    {
        if (string.IsNullOrWhiteSpace(javapExe) || string.IsNullOrWhiteSpace(meshHostOfficialOuter))
        {
            return null;
        }

        foreach (var companion in MeshHostClassCandidates.EnumerateAbstractCompanionFqns(meshHostOfficialOuter))
        {
            var block = TryDisassembleAndExtractMeshFactoryBlock(javapExe, clientJar, maps, companion, signatureNeedle);
            if (block is not null)
            {
                return block;
            }
        }

        foreach (var sup in EnumerateDeclaredSuperclassChainOfficial(javapExe, clientJar, maps, meshHostOfficialOuter,
                     meshHostClassJavapStdout, maxHops: 12))
        {
            if (string.Equals(sup, meshHostOfficialOuter, StringComparison.Ordinal))
            {
                continue;
            }

            var block = TryDisassembleAndExtractMeshFactoryBlock(javapExe, clientJar, maps, sup, signatureNeedle);
            if (block is not null)
            {
                return block;
            }
        }

        return null;
    }

    private static string? TryDisassembleAndExtractMeshFactoryBlock(string? javapExe, string clientJar,
        MojangMappingsParser? maps, string officialOuter, string signatureNeedle)
    {
        string? obh = null;
        _ = maps?.TryGetObfuscated(officialOuter, out obh);
        var javapArg = obh is null ? officialOuter : MojangMappingsParser.GetJavapClassArgForObfuscated(obh);
        if (!TryDisassemble(javapExe, clientJar, javapArg, out var stdout, out _))
        {
            return null;
        }

        var b = ExtractMethodCodeBlockBySignatureNeedle(stdout, signatureNeedle);
        if (b is not null && JavapMeshBytecodeProfiles.ContainsMeshSignals(b))
        {
            return b;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDeclaredSuperclassChainOfficial(string? javapExe, string clientJar,
        MojangMappingsParser? maps, string meshHostOfficialOuter, string meshHostClassJavapStdout, int maxHops)
    {
        var cur = meshHostOfficialOuter;
        var useCachedHostStdout = true;
        for (var hop = 0; hop < maxHops; hop++)
        {
            string stdout;
            if (useCachedHostStdout)
            {
                stdout = meshHostClassJavapStdout;
                useCachedHostStdout = false;
            }
            else
            {
                string? obh = null;
                _ = maps?.TryGetObfuscated(cur, out obh);
                var javapArg = obh is null ? cur : MojangMappingsParser.GetJavapClassArgForObfuscated(obh);
                if (!TryDisassemble(javapExe, clientJar, javapArg, out stdout, out _))
                {
                    yield break;
                }
            }

            var sup = TryParseDeclaredDirectSuperclassOfficial(stdout);
            if (string.IsNullOrEmpty(sup) || sup.StartsWith("java.", StringComparison.Ordinal))
            {
                yield break;
            }

            yield return sup;
            cur = sup;
        }
    }

    private static string? TryParseDeclaredDirectSuperclassOfficial(string javapC)
    {
        var m = JavaClassExtendsRegex.Match(javapC);
        if (!m.Success)
        {
            return null;
        }

        return m.Groups[1].Value.Replace("$", ".", StringComparison.Ordinal);
    }
}
