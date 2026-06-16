using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class BytecodeMeshResolution
{
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
            return BytecodeGeometryMeshLift.ConcatMeshFactoryCodeFromClass(hostClassBytes, maps);
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

            var nested = string.Empty;
            var ok = false;
            if (!string.Equals(owner, hostOfficialJvmName, StringComparison.Ordinal))
            {
                // HumanoidModel.createMesh is a single-method prelude; deep concat pulls armor/lambda islands
                // that reference_java bakes never include (skeleton, drowned, stray, bogged, …).
                if (UseShallowDelegatedMeshPrelude(owner, meth))
                {
                    nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [meth], out ok);
                }
                else
                {
                    nested = BuildMeshConcatDeep(clientJar, maps, owner, remoteBytes, meth);
                    ok = nested.Length > 0;
                }
            }

            if (!ok)
            {
                nested = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(remoteBytes, [meth], out ok);
            }

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
                TryLoadClassBytes(clientJar, maps, hostOfficialJvmName, out _))
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

        // Pull host void helpers before the host layer so createBodyMesh-style factories still expose leg cuboids.
        // The final override pass below appends marked helper islands again when needed so replacements win.
        AppendHostVoidMeshHelpersFromLayer(layer, insertIslandBoundaryBeforeNested: sb.Length > 0);

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

                _ = TryAppendCompanionMeshHelperIsland(r.Method, sig, insertIslandBoundaryBeforeNested: true);
                continue;
            }

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

        AppendMeshTransformerLambdaIslands();

        // Void helpers (e.g. SkeletonModel.createDefaultSkeletonMesh) must stay the final island so arm/leg
        // addOrReplaceChild overrides win over prepended HumanoidModel.createMesh defaults.
        AppendVoidMeshHelperTargets(sb.ToString(), insertIslandBoundaryBeforeNested: true);

        return NormalizeMeshIslandBoundaries(sb.ToString());

        static bool UseShallowDelegatedMeshPrelude(string ownerOfficial, string methodName) =>
            string.Equals(methodName, "createMesh", StringComparison.Ordinal) &&
            string.Equals(ownerOfficial, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal);

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
}
