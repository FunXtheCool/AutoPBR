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
    /// <summary>
    /// Named-jar mesh factory concatenation (Mojang source names in <c>javap</c> output):
    /// <c>createBodyLayer</c>, other static <c>MeshDefinition</c> helpers, then static <c>LayerDefinition</c> factories.
    /// </summary>
    public static string ConcatMeshFactoryCodeNamed(string javapC)
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AppendBlock(string methodName)
        {
            if (!seen.Add(methodName))
            {
                return;
            }

            var b = ExtractMethodCodeBlock(javapC, methodName);
            if (b is not null && JavapMeshBytecodeProfiles.ContainsMeshSignals(b))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine(GeometryMeshIslandBoundaryMarker);
                }

                sb.AppendLine(b);
            }
        }

        AppendBlock("createBodyLayer");

        foreach (Match m in StaticMeshDefinitionMethodDeclRegex.Matches(javapC))
        {
            var name = m.Groups[1].Value;
            if (string.Equals(name, "createBodyLayer", StringComparison.Ordinal))
            {
                continue;
            }

            AppendBlock(name);
        }

        foreach (Match m in StaticLayerDefinitionMethodDeclRegex.Matches(javapC))
        {
            AppendBlock(m.Groups[1].Value);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Concatenates mesh factory <c>Code:</c> blocks for <paramref name="officialOuterForMeshFactories"/> using ProGuard
    /// method pins when <paramref name="maps"/> is non-null; otherwise <see cref="ConcatMeshFactoryCodeNamed"/>.
    /// </summary>
    public static string ConcatMeshFactoryCode(string javapC, string? officialOuterForMeshFactories,
        MojangMappingsParser? maps)
    {
        if (maps is null || string.IsNullOrEmpty(officialOuterForMeshFactories))
        {
            return ConcatMeshFactoryCodeNamed(javapC);
        }

        var sb = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pin in maps.EnumerateMeshFactoryPins(officialOuterForMeshFactories))
        {
            var inner = pin.JavapParameterList;
            var sig = $" {pin.ObfuscatedMethod}({inner});";
            if (!seen.Add(sig))
            {
                continue;
            }

            var b = ExtractMethodCodeBlockBySignatureNeedle(javapC, sig);
            if (b is not null && JavapMeshBytecodeProfiles.ContainsMeshSignals(b))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine(GeometryMeshIslandBoundaryMarker);
                }

                sb.AppendLine(b);
            }
        }

        return sb.ToString();
    }

    /// <summary>Backward-compatible overload for named bytecode only.</summary>
    public static string ConcatMeshFactoryCode(string javapC) =>
        ConcatMeshFactoryCode(javapC, null, null);
}
