using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Lifts geometry IR by disassembling raw JVM method bytecode (exact constant pool floats) and reusing
/// <see cref="JavapFloatGeometryMeshLift"/> segment parsing on synthetic javap-style lines.
/// </summary>
internal static class BytecodeGeometryMeshLift
{
    internal static readonly string[] DefaultMeshFactoryMethods =
    [
        "createBodyLayer",
        "createBabyMesh",
        "createBabyLayer",
        "createBaseChickenModel",
        "createMesh",
        "createCapeLayer",
        "createHeadLayer",
        "createHatLayer",
        "createTranslucentBodyLayer",
        "createSaddleLayer",
        "createHarnessLayer",
    ];

    public static bool TryLift(
        ReadOnlySpan<byte> classFile,
        string factoryMethod,
        MojangMappingsParser? maps,
        out JsonArray roots,
        out List<string> notes) =>
        TryLiftConcat(BuildSyntheticMeshConcat(classFile, [factoryMethod], out _), maps, out roots, out notes);

    public static bool TryLiftConcat(
        string syntheticJavap,
        MojangMappingsParser? maps,
        out JsonArray roots,
        out List<string> notes,
        ReadOnlySpan<byte> hostClassBytes = default)
    {
        roots = [];
        notes = [];
        if (string.IsNullOrWhiteSpace(syntheticJavap))
        {
            notes.Add("Empty synthetic bytecode text.");
            return false;
        }

        IReadOnlyDictionary<string, int[][]>? matrices = null;
        IReadOnlyDictionary<string, float[]>? floatArrays = null;
        if (!hostClassBytes.IsEmpty)
        {
            matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(hostClassBytes);
            floatArrays = JvmStaticFloatArrayExtractor.ExtractFromClass(hostClassBytes);
        }

        var delegationDepth = syntheticJavap.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker).Length - 1;
        if (!JavapFloatGeometryMeshLift.TryLift(syntheticJavap, out roots, out notes, maps, delegationDepth,
                matrices, floatArrays))
        {
            notes.Insert(0, "Bytecode disassembly produced lines but segment lift failed.");
            return false;
        }

        roots = GeometryIrLiftTreeRepair.Apply(roots, hoistStandardQuadrupedLegsToRoot: false);
        notes.Insert(0, "Lifted via bytecode disassembly + segment parser (exact pool constants).");
        return CountCuboidsInRoots(roots) > 0;
    }

    private static int CountCuboidsInRoots(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["cuboids"] is JsonArray c)
            {
                n += c.Count;
            }

            if (p["children"] is JsonArray ch)
            {
                n += CountCuboidsInRoots(ch);
            }
        }

        return n;
    }

    public static bool TryLiftFromJar(
        string clientJar,
        string officialJvmName,
        string factoryMethod,
        MojangMappingsParser? maps,
        out JsonArray roots,
        out List<string> notes,
        out string? hostJvmName)
    {
        roots = [];
        notes = [];
        hostJvmName = null;
        if (!BytecodeMeshResolution.TryResolve(clientJar, maps, officialJvmName, factoryMethod, out var resolved))
        {
            notes.Add("Bytecode mesh resolution found no mesh factory in jar.");
            return false;
        }

        hostJvmName = resolved.HostJvmName;
        return TryLiftConcat(resolved.MeshConcat, maps, out roots, out notes, resolved.PrimaryClassBytes);
    }

    public static string BuildSyntheticMeshConcat(
        ReadOnlySpan<byte> classFile,
        IReadOnlyList<string> methodsToTry,
        out bool anyMethod)
    {
        anyMethod = false;
        var allLines = new List<string>();
        var tried = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methodsToTry)
        {
            if (!tried.Add(method))
            {
                continue;
            }

            if (!JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(classFile, method, out var lines) ||
                lines.Count == 0)
            {
                continue;
            }

            anyMethod = true;
            if (allLines.Count > 0)
            {
                allLines.Add(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker);
            }

            allLines.Add("    Code:");
            foreach (var line in lines)
            {
                var insn = line.TrimStart();
                if (!insn.Contains(':'))
                {
                    continue;
                }

                allLines.Add("     " + insn);
            }
        }

        return anyMethod ? string.Join('\n', allLines) : string.Empty;
    }

    /// <summary>
    /// Disassembles every static mesh-factory method on the class (same coverage as <see cref="JavapClassDisassembly.ConcatMeshFactoryCodeNamed"/>).
    /// </summary>
    public static string ConcatMeshFactoryCodeFromClass(ReadOnlySpan<byte> classFile, MojangMappingsParser? maps = null)
    {
        var methods = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(classFile))
        {
            if (!isStatic || !JvmClassFileParser.IsMeshFactoryDescriptor(desc, maps))
            {
                continue;
            }

            if (seen.Add(name))
            {
                methods.Add(name);
            }
        }

        if (methods.Count == 0)
        {
            methods.AddRange(DefaultMeshFactoryMethods);
        }
        else
        {
            methods.Sort(static (a, b) =>
            {
                if (string.Equals(a, "createBodyLayer", StringComparison.Ordinal))
                {
                    return -1;
                }

                if (string.Equals(b, "createBodyLayer", StringComparison.Ordinal))
                {
                    return 1;
                }

                return string.Compare(a, b, StringComparison.Ordinal);
            });
        }

        return BuildSyntheticMeshConcat(classFile, methods, out _);
    }

    public static string? TryExtractMethodBlockFromClass(ReadOnlySpan<byte> classFile, string signatureNeedle)
    {
        if (!TryParseMethodNameFromSignatureNeedle(signatureNeedle, out var methodName))
        {
            return null;
        }

        var block = BuildSyntheticMeshConcat(classFile, [methodName], out var ok);
        return ok && JavapMeshBytecodeProfiles.ContainsMeshSignals(block) ? block : null;
    }

    internal static bool TryParseMethodNameFromSignatureNeedle(string signatureNeedle, out string methodName)
    {
        methodName = string.Empty;
        var trimmed = signatureNeedle.Trim();
        var space = trimmed.IndexOf(' ');
        var decl = space >= 0 ? trimmed[(space + 1)..] : trimmed;
        var paren = decl.IndexOf('(');
        if (paren <= 0)
        {
            return false;
        }

        methodName = decl[..paren];
        return methodName.Length > 0;
    }

}
