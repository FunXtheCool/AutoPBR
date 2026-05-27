using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class BytecodeMeshResolution
{
    internal static bool ShouldSkipMeshHostWithoutPrimaryFactory(string hostOfficialJvmName,
        ReadOnlySpan<byte> hostClassBytes, string factoryMethod, MojangMappingsParser? maps = null)
    {
        if (!string.Equals(factoryMethod, "createBodyLayer", StringComparison.Ordinal) &&
            !string.Equals(factoryMethod, "createMesh", StringComparison.Ordinal))
        {
            return false;
        }

        var simple = hostOfficialJvmName[(hostOfficialJvmName.LastIndexOf('.') + 1)..];
        if (!simple.StartsWith("Abstract", StringComparison.Ordinal))
        {
            return false;
        }

        // ProGuard jars use short method names (e.g. createBodyLayer(float) -> a); match by return type, not Java name.
        if (JvmClassFileParser.HasStaticMeshFactoryMethod(hostClassBytes, maps))
        {
            return false;
        }

        return true;
    }

    private static bool IsDelegateOnlyLayerDefinitionFactory(string layerCode) =>
        HasNonTerminalLayerDefinitionFactoryInvoke(layerCode) &&
        !InvokeStaticReturnsMeshDefinitionCommentRegex.IsMatch(layerCode) &&
        !layerCode.Contains("addOrReplaceChild", StringComparison.Ordinal);

    /// <summary>
    /// Mesh hosts that build the part tree inline (e.g. <c>BabyZombieModel.createBodyLayer</c>) without delegating to
    /// <c>HumanoidModel.createMesh</c>; deep cross-class pulls would prepend unrelated humanoid islands.
    /// </summary>
    private static bool IsStandalonePartTreeMeshFactory(string layerCode)
    {
        if (!layerCode.Contains("addOrReplaceChild", StringComparison.Ordinal) ||
            InvokeStaticReturnsMeshDefinitionCommentRegex.IsMatch(layerCode) ||
            HasNonTerminalLayerDefinitionFactoryInvoke(layerCode))
        {
            return false;
        }

        // createBodyMesh-style factories that delegate cuboids to void helpers (createLegs, etc.) need deep concat.
        var folded = string.Join('\n', JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
            layerCode.Split('\n').Select(l => l.TrimEnd('\r')).ToList()));
        return !JavapMeshBytecodeProfiles.EnumerateInvokeStaticVoidMeshHelperRefs(folded).Any();
    }

    private static bool HasNonTerminalLayerDefinitionFactoryInvoke(string layerCode)
    {
        foreach (Match m in InvokeStaticReturnsLayerDefinitionCommentRegex.Matches(layerCode))
        {
            if (IsTerminalLayerDefinitionCreate(m.Groups[1].Value, m.Groups[2].Value))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsTerminalLayerDefinitionCreate(string ownerGroup, string methodName)
    {
        if (!string.Equals(methodName, "create", StringComparison.Ordinal))
        {
            return false;
        }

        var owner = ownerGroup.Replace('/', '.');
        return string.Equals(owner, "net.minecraft.client.model.geom.builders.LayerDefinition",
            StringComparison.Ordinal);
    }

    private static string? TryExtractNullOwnerStaticMeshFromHostSupertypes(
        string clientJar,
        MojangMappingsParser? maps,
        string meshHostOfficialOuter,
        byte[] meshHostClassBytes,
        string signatureNeedle)
    {
        foreach (var companion in MeshHostClassCandidates.EnumerateAbstractCompanionFqns(meshHostOfficialOuter))
        {
            if (!TryLoadClassBytes(clientJar, maps, companion, out var companionBytes))
            {
                continue;
            }

            var block = BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(companionBytes, signatureNeedle);
            if (block is not null)
            {
                return block;
            }
        }

        foreach (var sup in EnumerateSuperclassChain(clientJar, maps, meshHostOfficialOuter, meshHostClassBytes, 12))
        {
            if (string.Equals(sup, meshHostOfficialOuter, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryLoadClassBytes(clientJar, maps, sup, out var supBytes))
            {
                continue;
            }

            var block = BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(supBytes, signatureNeedle);
            if (block is not null)
            {
                return block;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSuperclassChain(
        string clientJar,
        MojangMappingsParser? maps,
        string startOfficial,
        byte[] startBytes,
        int maxHops)
    {
        var curBytes = startBytes;
        for (var hop = 0; hop < maxHops; hop++)
        {
            if (!JvmClassFileParser.TryGetSuperClassName(curBytes, out var sup) ||
                string.IsNullOrEmpty(sup) ||
                sup.StartsWith("java.", StringComparison.Ordinal))
            {
                yield break;
            }

            yield return sup;
            if (!TryLoadClassBytes(clientJar, maps, sup, out var nextBytes))
            {
                yield break;
            }

            curBytes = nextBytes;
            if (string.Equals(sup, startOfficial, StringComparison.Ordinal))
            {
                yield break;
            }
        }
    }

    private static bool TryLoadClassBytes(
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName,
        out byte[] classBytes)
    {
        classBytes = [];
        string? obf = null;
        _ = maps?.TryGetObfuscated(officialJvmName, out obf);
        return ClientJarIO.TryResolveJarEntry(clientJar, officialJvmName, obf, out _, out classBytes);
    }

    private static bool ContainsLiftableMeshBindingLines(string concat)
    {
        foreach (var line in concat.Split('\n'))
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(line))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IslandDefinesPartTreeBindings(string islandBytecode) =>
        islandBytecode.Contains("addOrReplaceChild", StringComparison.Ordinal) ||
        islandBytecode.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal);

    /// <summary>
    /// Ensures <see cref="JavapClassDisassembly.GeometryMeshIslandBoundaryMarker"/> is never glued to the previous
    /// instruction line (breaks island split and segment lift for creeper-like inline factories).
    /// </summary>
    internal static string NormalizeMeshIslandBoundaries(string concat)
    {
        if (string.IsNullOrEmpty(concat))
        {
            return concat;
        }

        var marker = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker;
        var separated = concat.Replace(marker, "\n" + marker + "\n", StringComparison.Ordinal);
        while (separated.Contains("\n\n\n", StringComparison.Ordinal))
        {
            separated = separated.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        return separated;
    }

    private static void EnsureStringBuilderEndsWithNewline(StringBuilder sb)
    {
        if (sb.Length == 0)
        {
            return;
        }

        var last = sb[^1];
        if (last is not '\n' and not '\r')
        {
            sb.AppendLine();
        }
    }
}
