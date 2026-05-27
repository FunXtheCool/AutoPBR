using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Resolves deep mesh-factory bytecode (multi-class) without javap for structural lift.
/// Mirrors <see cref="JavapClassDisassembly.ConcatMeshFactoryCodeDeep"/> using classfile disassembly only.
/// </summary>
internal static partial class BytecodeMeshResolution
{
    private static readonly Regex InvokeStaticReturnsMeshDefinitionCommentRegex = new(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:([\w$/\.]+)\.)?([\w$]+):\([^)]*\)L[\w/$]+MeshDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticReturnsLayerDefinitionCommentRegex = new(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:([\w$/\.]+)\.)?([\w$]+):\([^)]*\)L[\w/$]+LayerDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticObfuscatedReturnCommentRegex = new(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:([\w$/\.]+)\.)?([\w$]+):\([^)]*\)L(\w+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// ASM/javap comments for same-class <c>invokestatic</c> omit the owner prefix (<c>// Method createBodyMesh:(…)</c>).
    /// </summary>
    private static string ResolveInvokeStaticOwnerOrHost(string ownerGroup, string hostOfficialJvmName)
    {
        var owner = ownerGroup.Replace('/', '.');
        return string.IsNullOrEmpty(owner) ? hostOfficialJvmName : owner;
    }

    /// <summary>
    /// Loads the class that actually declares <paramref name="staticMethodName"/> (explicit owner comment, then host supertypes).
    /// </summary>
    private static bool TryLoadClassBytesDeclaringStaticMethod(
        string clientJar,
        MojangMappingsParser? maps,
        string hostOfficialJvmName,
        byte[] hostClassBytes,
        string preferredOwnerOrEmpty,
        string staticMethodName,
        out string declaringOwner,
        out byte[] declaringBytes)
    {
        declaringOwner = string.Empty;
        declaringBytes = [];
        if (!string.IsNullOrEmpty(preferredOwnerOrEmpty))
        {
            var explicitOwner = preferredOwnerOrEmpty.Replace('/', '.');
            if (TryLoadClassBytes(clientJar, maps, explicitOwner, out var explicitBytes) &&
                JvmClassFileParser.TryGetMethodCode(explicitBytes, staticMethodName) is not null)
            {
                declaringOwner = explicitOwner;
                declaringBytes = explicitBytes;
                return true;
            }
        }

        foreach (var candidate in EnumerateSuperclassChain(clientJar, maps, hostOfficialJvmName, hostClassBytes, 12))
        {
            if (!TryLoadClassBytes(clientJar, maps, candidate, out var bytes))
            {
                continue;
            }

            if (JvmClassFileParser.TryGetMethodCode(bytes, staticMethodName) is not null)
            {
                declaringOwner = candidate;
                declaringBytes = bytes;
                return true;
            }
        }

        return false;
    }

    public readonly record struct Result(string HostJvmName, string MeshConcat, byte[] PrimaryClassBytes);

    public static bool TryResolve(
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName,
        string factoryMethod,
        out Result result)
    {
        result = default!;
        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            string? obh = null;
            _ = maps?.TryGetObfuscated(host, out obh);
            if (!ClientJarIO.TryResolveJarEntry(clientJar, host, obh, out _, out var classBytes))
            {
                continue;
            }

            if (ShouldSkipMeshHostWithoutPrimaryFactory(host, classBytes, factoryMethod, maps))
            {
                continue;
            }

            var concat = BuildMeshConcatDeep(clientJar, maps, host, classBytes, factoryMethod);
            if (string.IsNullOrEmpty(concat) ||
                !JavapMeshBytecodeProfiles.ContainsMeshSignals(concat) ||
                !ContainsLiftableMeshBindingLines(concat))
            {
                continue;
            }

            result = new Result(host, concat, classBytes);
            return true;
        }

        return false;
    }

    private static readonly string[] SupplementaryLayerFactoryMethods =
    [
        "createWindLayer",
        "createEyesLayer",
    ];

    private static List<string> CollectFactoryMethodNames(
        MojangMappingsParser? maps,
        string hostOfficialJvmName,
        string factoryMethod,
        ReadOnlySpan<byte> hostClassBytes)
    {
        var names = new List<string>();
        void Add(string name)
        {
            if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        if (maps is not null)
        {
            foreach (var pin in maps.EnumerateMeshFactoryPins(hostOfficialJvmName))
            {
                Add(pin.ObfuscatedMethod);
            }
        }

        Add(factoryMethod);
        Add("createBodyLayer");
        Add("createMesh");
        Add("apply");
        Add("createCapeLayer");
        Add("createHeadModel");
        Add("createBodyMesh");
        Add("createSingleBodyLayer");
        Add("createDoubleBodyRightLayer");
        Add("createDoubleBodyLeftLayer");

        if (!hostClassBytes.IsEmpty)
        {
            foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(hostClassBytes))
            {
                if (!isStatic)
                {
                    continue;
                }

                if (JvmClassFileParser.IsMeshFactoryDescriptor(desc, maps))
                {
                    Add(name);
                    continue;
                }

                foreach (var supplementary in SupplementaryLayerFactoryMethods)
                {
                    if (string.Equals(name, supplementary, StringComparison.Ordinal))
                    {
                        Add(name);
                    }
                }
            }
        }

        return names;
    }
}

