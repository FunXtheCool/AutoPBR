using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Resolves deep mesh-factory bytecode (multi-class) without javap for structural lift.
/// Mirrors <see cref="JavapClassDisassembly.ConcatMeshFactoryCodeDeep"/> using classfile disassembly only.
/// </summary>
internal static class BytecodeMeshResolution
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

            if (ShouldSkipMeshHostWithoutPrimaryFactory(host, classBytes, factoryMethod))
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
                if (!isStatic ||
                    !desc.Contains("LayerDefinition", StringComparison.Ordinal) ||
                    string.Equals(name, factoryMethod, StringComparison.Ordinal))
                {
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

    internal static string BuildMeshConcatDeep(
        string clientJar,
        MojangMappingsParser? maps,
        string hostOfficialJvmName,
        byte[] hostClassBytes,
        string factoryMethod)
    {
        var factoryNames = CollectFactoryMethodNames(maps, hostOfficialJvmName, factoryMethod, hostClassBytes);
        var layer = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(
            hostClassBytes,
            factoryNames,
            out _);

        if (string.IsNullOrEmpty(layer))
        {
            return BytecodeGeometryMeshLift.ConcatMeshFactoryCodeFromClass(hostClassBytes);
        }

        if (IsStandalonePartTreeMeshFactory(layer))
        {
            return layer;
        }

        if (IsDelegateOnlyLayerDefinitionFactory(layer))
        {
            foreach (Match m in InvokeStaticReturnsLayerDefinitionCommentRegex.Matches(layer))
            {
                var meth = m.Groups[2].Value;
                if (!TryLoadClassBytesDeclaringStaticMethod(clientJar, maps, hostOfficialJvmName, hostClassBytes,
                        m.Groups[1].Value, meth, out var owner, out var delegateBytes))
                {
                    continue;
                }

                return BuildMeshConcatDeep(clientJar, maps, owner, delegateBytes, meth);
            }
        }

        var seenOwners = new HashSet<string>(StringComparer.Ordinal);
        var createBasePrelude = new List<string>();

        void AppendCreateBaseMeshFromSupertypes()
        {
            foreach (var sup in EnumerateSuperclassChain(clientJar, maps, hostOfficialJvmName, hostClassBytes, 8))
            {
                if (!TryLoadClassBytes(clientJar, maps, sup, out var supBytes))
                {
                    continue;
                }

                foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(supBytes))
                {
                    if (!isStatic ||
                        !name.StartsWith("createBase", StringComparison.Ordinal) ||
                        !desc.Contains("MeshDefinition", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var key = sup + "::" + name;
                    if (!seenOwners.Add(key))
                    {
                        continue;
                    }

                    var nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(supBytes, [name], out var ok);
                    if (!ok || string.IsNullOrWhiteSpace(nested))
                    {
                        continue;
                    }

                    createBasePrelude.Add(nested);
                }
            }
        }

        AppendCreateBaseMeshFromSupertypes();
        var sb = new StringBuilder();
        foreach (var block in createBasePrelude)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker);
            }

            sb.AppendLine(block);
        }

        void PrependMeshDefinitionBlock(string owner, string meth, byte[] remoteBytes)
        {
            var key = owner + "::" + meth;
            if (!seenOwners.Add(key))
            {
                return;
            }

            var nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [meth], out var ok);
            if (!ok || string.IsNullOrWhiteSpace(nested))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker);
            }

            sb.AppendLine(nested);
            _ = seenOwners.Add("pulled:" + owner);
        }

        foreach (Match m in InvokeStaticReturnsMeshDefinitionCommentRegex.Matches(layer))
        {
            var meth = m.Groups[2].Value;
            if (!TryLoadClassBytesDeclaringStaticMethod(clientJar, maps, hostOfficialJvmName, hostClassBytes,
                    m.Groups[1].Value, meth, out var owner, out var remoteBytes))
            {
                continue;
            }

            PrependMeshDefinitionBlock(owner, meth, remoteBytes);
        }

        var deferHostLayerUntilAfterPulls = IsDelegateOnlyLayerDefinitionFactory(layer);

        void AppendNestedBytecode(string nested, bool insertIslandBoundaryBeforeNested)
        {
            if (nested.Length == 0)
            {
                return;
            }

            if (insertIslandBoundaryBeforeNested && sb.Length > 0)
            {
                EnsureStringBuilderEndsWithNewline(sb);
                sb.AppendLine(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker);
            }

            sb.AppendLine(nested);
        }

        void AppendNestedWithVoidHelpers(string nested, bool insertIslandBoundaryBeforeNested)
        {
            if (nested.Length == 0)
            {
                return;
            }

            AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested);
        }

        void TryPullInvokeStaticMeshTarget(string ownerSlashOrEmpty, string meth, bool insertIslandBoundaryBeforeNested)
        {
            if (!TryLoadClassBytesDeclaringStaticMethod(clientJar, maps, hostOfficialJvmName, hostClassBytes,
                    ownerSlashOrEmpty, meth, out var owner, out var remoteBytes))
            {
                return;
            }

            if (!seenOwners.Add(owner + "::" + meth))
            {
                return;
            }

            var nested = "";
            var ok = false;
            if (!string.Equals(owner, hostOfficialJvmName, StringComparison.Ordinal) &&
                string.Equals(meth, "createBodyLayer", StringComparison.Ordinal))
            {
                nested = BuildMeshConcatDeep(clientJar, maps, owner, remoteBytes, meth);
                ok = nested.Length > 0;
            }

            if (!ok)
            {
                nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [meth], out ok);
            }

            if (nested.Length == 0 &&
                TryLoadClassBytes(clientJar, maps, hostOfficialJvmName, out var hostBytes))
            {
                foreach (var sup in EnumerateSuperclassChain(clientJar, maps, owner, remoteBytes, 8))
                {
                    if (!TryLoadClassBytes(clientJar, maps, sup, out var supBytes))
                    {
                        continue;
                    }

                    nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(supBytes, [meth], out ok);
                    if (ok && !string.IsNullOrWhiteSpace(nested))
                    {
                        remoteBytes = supBytes;
                        owner = sup;
                        break;
                    }
                }
            }

            if (nested.Length == 0 || !IslandDefinesPartTreeBindings(nested))
            {
                return;
            }

            if (!string.Equals(owner, hostOfficialJvmName, StringComparison.Ordinal) &&
                string.Equals(meth, "createBodyLayer", StringComparison.Ordinal) &&
                nested.Contains(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringComparison.Ordinal))
            {
                AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested);
            }
            else
            {
                AppendNestedWithVoidHelpers(nested, insertIslandBoundaryBeforeNested);
            }

            _ = seenOwners.Add("pulled:" + owner);
        }

        void AppendMeshDefinitionInvokeTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            foreach (Match m in InvokeStaticReturnsMeshDefinitionCommentRegex.Matches(scan))
            {
                TryPullInvokeStaticMeshTarget(m.Groups[1].Value, m.Groups[2].Value, insertIslandBoundaryBeforeNested);
            }

            if (maps is null)
            {
                return;
            }

            foreach (Match m in InvokeStaticObfuscatedReturnCommentRegex.Matches(scan))
            {
                var retShort = m.Groups[3].Value;
                if (!maps.TryIsObfuscatedReturnType(retShort, "MeshDefinition"))
                {
                    continue;
                }

                TryPullInvokeStaticMeshTarget(m.Groups[1].Value, m.Groups[2].Value, insertIslandBoundaryBeforeNested);
            }
        }

        void AppendLayerDefinitionInvokeTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            foreach (Match m in InvokeStaticReturnsLayerDefinitionCommentRegex.Matches(scan))
            {
                TryPullInvokeStaticMeshTarget(m.Groups[1].Value, m.Groups[2].Value, insertIslandBoundaryBeforeNested);
            }
        }

        bool TryAppendCompanionMeshHelperIsland(string method, string signatureNeedle,
            bool insertIslandBoundaryBeforeNested)
        {
            if (BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(hostClassBytes, signatureNeedle) is
                { } hostBlock &&
                JavapMeshBytecodeProfiles.ContainsMeshSignals(hostBlock) &&
                CompanionMeshHelperLiftsPartTree(hostBlock))
            {
                AppendNestedBytecode(hostBlock, insertIslandBoundaryBeforeNested);
                return true;
            }

            foreach (var companion in MeshHostClassCandidates.EnumerateAbstractCompanionFqns(hostOfficialJvmName))
            {
                if (!TryLoadClassBytes(clientJar, maps, companion, out var companionBytes))
                {
                    continue;
                }

                var nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(companionBytes, [method], out var ok);
                if (ok &&
                    !string.IsNullOrWhiteSpace(nested) &&
                    JavapMeshBytecodeProfiles.ContainsMeshSignals(nested))
                {
                    AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested);
                    return true;
                }

                var block = BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(companionBytes, signatureNeedle);
                if (block is not null &&
                    JavapMeshBytecodeProfiles.ContainsMeshSignals(block) &&
                    CompanionMeshHelperLiftsPartTree(block))
                {
                    AppendNestedBytecode(block, insertIslandBoundaryBeforeNested);
                    return true;
                }
            }

            var fallback = TryExtractNullOwnerStaticMeshFromHostSupertypes(clientJar, maps, hostOfficialJvmName,
                hostClassBytes, signatureNeedle);
            if (fallback is not null &&
                JavapMeshBytecodeProfiles.ContainsMeshSignals(fallback) &&
                CompanionMeshHelperLiftsPartTree(fallback))
            {
                AppendNestedBytecode(fallback, insertIslandBoundaryBeforeNested);
                return true;
            }

            return false;
        }

        static bool CompanionMeshHelperLiftsPartTree(string islandBytecode)
        {
            if (!JavapFloatGeometryMeshLift.TryLift(islandBytecode, out var roots, out _, maps: null))
            {
                return false;
            }

            foreach (var n in roots)
            {
                if (n is not System.Text.Json.Nodes.JsonObject o)
                {
                    continue;
                }

                if (PartTreeHasId(o, "head") || PartTreeHasId(o, "left_ear"))
                {
                    return true;
                }
            }

            return false;
        }

        static bool PartTreeHasId(System.Text.Json.Nodes.JsonObject part, string id)
        {
            if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
            {
                return true;
            }

            if (part["children"] is not System.Text.Json.Nodes.JsonArray kids)
            {
                return false;
            }

            foreach (var ch in kids)
            {
                if (ch is System.Text.Json.Nodes.JsonObject co && PartTreeHasId(co, id))
                {
                    return true;
                }
            }

            return false;
        }

        void TryPullVoidHelperMethod(string ownerOfficial, string method, bool insertIslandBoundaryBeforeNested)
        {
            var key = ownerOfficial + "::" + method + ":void";
            if (!seenOwners.Add(key))
            {
                return;
            }

            if (!TryLoadClassBytes(clientJar, maps, ownerOfficial, out var remoteBytes))
            {
                return;
            }

            var block = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [method], out var ok);
            if (!ok || string.IsNullOrEmpty(block) || !JavapMeshBytecodeProfiles.ContainsMeshSignals(block))
            {
                return;
            }

            block = $"// __AUTOPBR_VOID_MESH_HELPER__ {ownerOfficial}.{method}\n{block}";
            AppendNestedBytecode(block, insertIslandBoundaryBeforeNested);
        }

        void AppendHostVoidMeshHelpersFromLayer(string scan, bool insertIslandBoundaryBeforeNested)
        {
            var foldedScan = string.Join('\n', JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
                scan.Split('\n').Select(l => l.TrimEnd('\r')).ToList()));
            foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticVoidMeshHelperRefs(foldedScan))
            {
                var owner = r.OwnerJarSimple?.Replace('/', '.');
                if (owner is not null &&
                    !string.Equals(owner, hostOfficialJvmName, StringComparison.Ordinal))
                {
                    continue;
                }

                var key = hostOfficialJvmName + "::" + r.Method + ":hostvoid";
                if (!seenOwners.Add(key))
                {
                    continue;
                }

                var block = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(hostClassBytes, [r.Method], out var ok);
                if (!ok || string.IsNullOrEmpty(block) ||
                    !JavapMeshBytecodeProfiles.ContainsMeshSignals(block))
                {
                    continue;
                }

                AppendNestedBytecode(block, insertIslandBoundaryBeforeNested);
            }
        }

        void AppendVoidMeshHelperTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            var foldedScan = string.Join('\n', JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
                scan.Split('\n').Select(l => l.TrimEnd('\r')).ToList()));
            foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticVoidMeshHelperRefs(foldedScan))
            {
                var owner = r.OwnerJarSimple?.Replace('/', '.') ?? hostOfficialJvmName;
                if (string.Equals(owner, hostOfficialJvmName, StringComparison.Ordinal) &&
                    seenOwners.Contains(hostOfficialJvmName + "::" + r.Method + ":hostvoid"))
                {
                    continue;
                }

                TryPullVoidHelperMethod(owner, r.Method, insertIslandBoundaryBeforeNested);
            }
        }

        AppendMeshDefinitionInvokeTargets(layer, insertIslandBoundaryBeforeNested: true);
        AppendLayerDefinitionInvokeTargets(layer, insertIslandBoundaryBeforeNested: true);
        for (var iter = 0; iter < 16; iter++)
        {
            var mark = sb.Length;
            var scan = sb.ToString();
            AppendMeshDefinitionInvokeTargets(scan, insertIslandBoundaryBeforeNested: false);
            AppendLayerDefinitionInvokeTargets(scan, insertIslandBoundaryBeforeNested: false);
            if (sb.Length == mark)
            {
                break;
            }
        }

        // Pull void helpers (e.g. QuadrupedModel.createLegs) from the host layer before it is appended so leg
        // cuboids are not lost when createBodyMesh is the only factory method in the concat.
        AppendHostVoidMeshHelpersFromLayer(layer, insertIslandBoundaryBeforeNested: sb.Length > 0);
        AppendVoidMeshHelperTargets(layer, insertIslandBoundaryBeforeNested: sb.Length > 0);

        if (!deferHostLayerUntilAfterPulls)
        {
            if (sb.Length > 0)
            {
                EnsureStringBuilderEndsWithNewline(sb);
                sb.AppendLine(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker);
            }

            sb.AppendLine(layer.TrimEnd('\r', '\n'));
        }

        foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(layer))
        {
            var own = r.OwnerJarSimple;
            var isHostCompanionStaticOnLayer = own is not null &&
                string.Equals(own, hostOfficialJvmName, StringComparison.Ordinal) &&
                JavapMeshBytecodeProfiles.IsVoidMeshHelperMethodName(r.Method);
            if (own is null || isHostCompanionStaticOnLayer)
            {
                var inner = r.ArgsInner;
                var sig = $" {r.Method}({inner});";
                if (!seenOwners.Add("same:" + sig))
                {
                    continue;
                }

                if (TryAppendCompanionMeshHelperIsland(r.Method, sig, insertIslandBoundaryBeforeNested: true))
                {
                    continue;
                }

                continue;
            }

            own = r.OwnerJarSimple;
            var methodKey = own + "::" + r.Method;
            if (seenOwners.Contains(methodKey))
            {
                continue;
            }

            if (!TryLoadClassBytes(clientJar, maps, own, out var remoteBytes))
            {
                continue;
            }

            var nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [r.Method], out var ok);
            if (!ok || string.IsNullOrWhiteSpace(nested) || !IslandDefinesPartTreeBindings(nested))
            {
                continue;
            }

            AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested: true);
            _ = seenOwners.Add(methodKey);
            _ = seenOwners.Add("pulled:" + own);
        }

        // Delegate-only hosts (e.g. AdultZombifiedPiglinModel → AdultPiglinModel.createBodyLayer) forward mesh
        // construction; omit the thin forwarder island so it cannot last-win merge over addHead/ears.

        EnsureHostCompanionAddHeadIsland(layer);

        // Void helpers (e.g. SkeletonModel.createDefaultSkeletonMesh) must stay the final island so arm/leg
        // addOrReplaceChild overrides win over prepended HumanoidModel.createMesh defaults.
        AppendVoidMeshHelperTargets(sb.ToString(), insertIslandBoundaryBeforeNested: true);

        AppendMeshTransformerLambdaIslands();

        return NormalizeMeshIslandBoundaries(sb.ToString());

        void AppendMeshTransformerLambdaIslands()
        {
            void TryAppendMeshTransformerIsland(string methodName, string markerSuffix)
            {
                var key = hostOfficialJvmName + "::" + methodName + ":" + markerSuffix;
                if (!seenOwners.Add(key))
                {
                    return;
                }

                var block = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(hostClassBytes, [methodName], out var ok);
                if (!ok || string.IsNullOrEmpty(block) || !JavapMeshBytecodeProfiles.ContainsMeshSignals(block))
                {
                    return;
                }

                block = $"// __AUTOPBR_MESH_TRANSFORMER_LAMBDA__ {hostOfficialJvmName}.{methodName}\n{block}";
                AppendNestedBytecode(block, insertIslandBoundaryBeforeNested: true);
            }

            foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(hostClassBytes))
            {
                if (!isStatic)
                {
                    continue;
                }

                if (name.Contains("lambda$", StringComparison.Ordinal) &&
                    desc.Contains("MeshDefinition", StringComparison.Ordinal))
                {
                    TryAppendMeshTransformerIsland(name, "meshtransformer-lambda");
                    continue;
                }

                if (string.Equals(name, "modifyMesh", StringComparison.Ordinal) ||
                    (name.Contains("modifyMesh", StringComparison.Ordinal) &&
                     desc.Contains("PartDefinition", StringComparison.Ordinal)))
                {
                    TryAppendMeshTransformerIsland(name, "meshtransformer-modify");
                }
            }
        }

        void EnsureHostCompanionAddHeadIsland(string layerCode)
        {
            foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(layerCode))
            {
                if (!string.Equals(r.Method, "addHead", StringComparison.Ordinal))
                {
                    continue;
                }

                var isHostInvoke = r.OwnerJarSimple is null ||
                    string.Equals(r.OwnerJarSimple, hostOfficialJvmName, StringComparison.Ordinal);
                var isAbstractCompanionInvoke = r.OwnerJarSimple is not null &&
                    MeshHostClassCandidates.EnumerateAbstractCompanionFqns(hostOfficialJvmName)
                        .Any(c => string.Equals(c, r.OwnerJarSimple, StringComparison.Ordinal));
                if (!isHostInvoke && !isAbstractCompanionInvoke)
                {
                    continue;
                }

                var sig = $" {r.Method}({r.ArgsInner});";
                foreach (var companion in MeshHostClassCandidates.EnumerateAbstractCompanionFqns(hostOfficialJvmName))
                {
                    if (!TryLoadClassBytes(clientJar, maps, companion, out var companionBytes))
                    {
                        continue;
                    }

                    var nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(companionBytes, ["addHead"], out var ok);
                    if (!ok || string.IsNullOrWhiteSpace(nested))
                    {
                        continue;
                    }

                    AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested: true);
                    return;
                }

                if (TryAppendCompanionMeshHelperIsland(r.Method, sig, insertIslandBoundaryBeforeNested: true))
                {
                    return;
                }
            }
        }
    }

    internal static bool ShouldSkipMeshHostWithoutPrimaryFactory(string hostOfficialJvmName,
        ReadOnlySpan<byte> hostClassBytes, string factoryMethod)
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

        foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(hostClassBytes))
        {
            if (!isStatic)
            {
                continue;
            }

            if (string.Equals(name, factoryMethod, StringComparison.Ordinal) &&
                (desc.Contains("LayerDefinition", StringComparison.Ordinal) ||
                 desc.Contains("MeshDefinition", StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDelegateOnlyLayerDefinitionFactory(string layerCode) =>
        InvokeStaticReturnsLayerDefinitionCommentRegex.IsMatch(layerCode) &&
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
            InvokeStaticReturnsLayerDefinitionCommentRegex.IsMatch(layerCode))
        {
            return false;
        }

        // createBodyMesh-style factories that delegate cuboids to void helpers (createLegs, etc.) need deep concat.
        var folded = string.Join('\n', JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
            layerCode.Split('\n').Select(l => l.TrimEnd('\r')).ToList()));
        return !JavapMeshBytecodeProfiles.EnumerateInvokeStaticVoidMeshHelperRefs(folded).Any();
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
