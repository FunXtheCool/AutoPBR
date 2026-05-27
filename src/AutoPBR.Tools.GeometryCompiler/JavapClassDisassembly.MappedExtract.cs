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
    /// First factory <c>Code:</c> block for any pin from <see cref="MojangMappingsParser.EnumerateMeshFactoryPins"/>
    /// (createBodyLayer, then <c>MeshDefinition</c> / <c>LayerDefinition</c> helpers).
    /// </summary>
    public static string? ExtractFirstMappedMeshFactoryCode(string javapStdout, MojangMappingsParser maps,
        string meshHostOfficialOuter)
    {
        foreach (var pin in maps.EnumerateMeshFactoryPins(meshHostOfficialOuter))
        {
            var inner = pin.JavapParameterList;
            var sig = $" {pin.ObfuscatedMethod}({inner});";
            var b = ExtractMethodCodeBlockBySignatureNeedle(javapStdout, sig);
            if (b is not null)
            {
                return b;
            }
        }

        return null;
    }

    /// <summary>Extracts the first mapped <c>createBodyLayer</c> bytecode block for ProGuard jars.</summary>
    public static string? ExtractFirstCreateBodyLayerCode(string javapStdout, MojangMappingsParser maps,
        string meshHostOfficialOuter)
    {
        foreach (var pin in maps.EnumerateMeshFactoryPins(meshHostOfficialOuter))
        {
            if (!string.Equals(pin.NamedMethod, "createBodyLayer", StringComparison.Ordinal))
            {
                continue;
            }

            var inner = pin.JavapParameterList;
            var sig = $" {pin.ObfuscatedMethod}({inner});";
            var b = ExtractMethodCodeBlockBySignatureNeedle(javapStdout, sig);
            if (b is not null)
            {
                return b;
            }
        }

        return ExtractMethodCodeBlock(javapStdout, "createBodyLayer");
    }
}
