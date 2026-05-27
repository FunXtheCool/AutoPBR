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
    /// Like <see cref="ConcatMeshFactoryCode(string, string?, MojangMappingsParser?)"/> for <paramref name="rootJavapStdout"/>, then pulls
    /// <c>invokestatic</c> targets that return <c>MeshDefinition</c> or <c>LayerDefinition</c> (iteratively on the accumulated bytecode text so
    /// delegates like <c>AdultZombifiedPiglinModel</c> → <c>AdultPiglinModel</c> → <c>PlayerModel.createMesh</c> resolve), plus same-class helpers and
    /// null-owner <c>invokestatic</c> mesh methods declared on an <c>Abstract*</c> companion (e.g. <c>AbstractPiglinModel.addHead</c> from <c>AdultPiglinModel</c>).
    /// </summary>
    public static string ConcatMeshFactoryCodeDeep(string? javapExe, string clientJar, string rootJavapStdout,
        string? meshHostOfficialOuter, MojangMappingsParser? maps, string meshHostJavapArg)
    {
        var layer = maps is not null && !string.IsNullOrEmpty(meshHostOfficialOuter)
            ? ExtractFirstMappedMeshFactoryCode(rootJavapStdout, maps, meshHostOfficialOuter)
            : null;
        layer ??= ExtractMethodCodeBlock(rootJavapStdout, "createBodyLayer");
        if (string.IsNullOrEmpty(layer))
        {
            return ConcatMeshFactoryCode(rootJavapStdout, meshHostOfficialOuter, maps);
        }

        var acc = layer;

        var seenOwners = new HashSet<string>(StringComparer.Ordinal);

        void AppendNestedBytecode(string nested, bool insertIslandBoundaryBeforeNested)
        {
            if (nested.Length == 0)
            {
                return;
            }

            acc += insertIslandBoundaryBeforeNested
                ? "\n" + GeometryMeshIslandBoundaryMarker + "\n" + nested
                : "\n" + nested;
        }

        /// <summary>
        /// Delegated <c>MeshDefinition</c> factories (e.g. <c>HumanoidModel.createMesh</c>) must lift before the outer
        /// <c>createBodyLayer</c> so island merge last-wins keeps <c>addOrReplaceChild</c> overrides from the host class.
        /// </summary>
        void PrependNestedBytecode(string nested, bool insertIslandBoundaryAfterNested)
        {
            if (nested.Length == 0)
            {
                return;
            }

            acc = insertIslandBoundaryAfterNested
                ? nested + "\n" + GeometryMeshIslandBoundaryMarker + "\n" + acc
                : nested + "\n" + acc;
        }

        void AppendMeshDefinitionInvokeTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            foreach (Match m in InvokeStaticReturnsMeshDefinitionCommentRegex.Matches(scan))
            {
                TryPullInvokeStaticMeshTarget(m.Groups[1].Value, m.Groups[2].Value, insertIslandBoundaryBeforeNested);
            }

            var hostOwner = meshHostOfficialOuter ?? meshHostJavapArg?.Replace('/', '.');
            if (!string.IsNullOrEmpty(hostOwner))
            {
                foreach (Match m in InvokeStaticSameClassMeshDefinitionCommentRegex.Matches(scan))
                {
                    TryPullInvokeStaticMeshTarget(hostOwner, m.Groups[1].Value, insertIslandBoundaryBeforeNested);
                }
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

        void TryPullInvokeStaticMeshTarget(string ownerSlash, string meth, bool insertIslandBoundaryBeforeNested,
            bool prependMeshDefinitionIsland = true)
        {
            var owner = ownerSlash.Replace('/', '.');
            if (!seenOwners.Add(owner + "::" + meth))
            {
                return;
            }

            if (!TryDisassemble(javapExe, clientJar, owner, out var remoteOut, out _))
            {
                return;
            }

            var nested = ExtractMethodCodeBlock(remoteOut, meth);
            if (string.IsNullOrEmpty(nested))
            {
                nested = ConcatMeshFactoryCode(remoteOut, owner, maps);
            }

            if (nested.Length == 0)
            {
                return;
            }

            if (prependMeshDefinitionIsland)
            {
                PrependNestedBytecode(nested, insertIslandBoundaryBeforeNested);
            }
            else
            {
                AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested);
            }

            _ = seenOwners.Add("pulled:" + owner);
        }

        void AppendLayerDefinitionInvokeTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            foreach (Match m in InvokeStaticReturnsLayerDefinitionCommentRegex.Matches(scan))
            {
                var owner = m.Groups[1].Value.Replace('/', '.');
                var meth = m.Groups[2].Value;
                if (!seenOwners.Add(owner + "::" + meth))
                {
                    continue;
                }

                if (!TryDisassemble(javapExe, clientJar, owner, out var remoteOut, out _))
                {
                    continue;
                }

                string? obh = null;
                _ = maps?.TryGetObfuscated(owner, out obh);
                var javapArg = obh is null ? owner : MojangMappingsParser.GetJavapClassArgForObfuscated(obh);
                var nested = string.Equals(owner, meshHostOfficialOuter, StringComparison.Ordinal) ||
                             !string.Equals(meth, "createBodyLayer", StringComparison.Ordinal)
                    ? ConcatMeshFactoryCode(remoteOut, owner, maps)
                    : ConcatMeshFactoryCodeDeep(javapExe, clientJar, remoteOut, owner, maps, javapArg);
                if (nested.Length == 0)
                {
                    continue;
                }

                AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested);
                _ = seenOwners.Add("pulled:" + owner);
            }
        }

        void AppendVoidMeshHelperTargets(string scan, bool insertIslandBoundaryBeforeNested)
        {
            foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticVoidMeshHelperRefs(scan))
            {
                var owner = r.OwnerJarSimple?.Replace('/', '.') ?? meshHostOfficialOuter;
                if (string.IsNullOrEmpty(owner))
                {
                    continue;
                }

                var key = owner + "::" + r.Method + ":void";
                if (!seenOwners.Add(key))
                {
                    continue;
                }

                if (!TryDisassemble(javapExe, clientJar, owner, out var remoteOut, out _))
                {
                    continue;
                }

                var b = ExtractMethodCodeBlock(remoteOut, r.Method);
                if (b is not null && JavapMeshBytecodeProfiles.ContainsMeshSignals(b))
                {
                    AppendNestedBytecode(b, insertIslandBoundaryBeforeNested);
                }
            }
        }

        AppendMeshDefinitionInvokeTargets(layer, insertIslandBoundaryBeforeNested: true);
        AppendLayerDefinitionInvokeTargets(layer, insertIslandBoundaryBeforeNested: true);
        for (var iter = 0; iter < 16; iter++)
        {
            var mark = acc.Length;
            AppendMeshDefinitionInvokeTargets(acc, insertIslandBoundaryBeforeNested: false);
            AppendLayerDefinitionInvokeTargets(acc, insertIslandBoundaryBeforeNested: false);
            if (acc.Length == mark)
            {
                break;
            }
        }

        foreach (var r in JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(layer))
        {
            var own = r.OwnerJarSimple;
            var isHostCompanionStatic = own is not null &&
                string.Equals(own, meshHostOfficialOuter, StringComparison.Ordinal) &&
                JavapMeshBytecodeProfiles.IsVoidMeshHelperMethodName(r.Method);
            if (own is null || isHostCompanionStatic)
            {
                var inner = r.ArgsInner;
                var sig = $" {r.Method}({inner});";
                if (!seenOwners.Add("same:" + sig))
                {
                    continue;
                }

                var b = ExtractMethodCodeBlockBySignatureNeedle(rootJavapStdout, sig);
                b ??= TryExtractNullOwnerStaticMeshFromHostSupertypes(javapExe, clientJar, maps, meshHostOfficialOuter,
                    rootJavapStdout, sig);
                if (b is not null && JavapMeshBytecodeProfiles.ContainsMeshSignals(b))
                {
                    AppendNestedBytecode(b, insertIslandBoundaryBeforeNested: true);
                }

                continue;
            }

            own = r.OwnerJarSimple;
            var methodKey = own + "::" + r.Method;
            if (seenOwners.Contains(methodKey))
            {
                continue;
            }

            if (!TryDisassemble(javapExe, clientJar, own, out var remoteOut, out _))
            {
                continue;
            }

            string? remoteOfficial = null;
            if (maps is not null && maps.TryGetNamedOuterFromJarSimple(own, out var namedOuter))
            {
                remoteOfficial = namedOuter;
            }

            var nested = ExtractMethodCodeBlock(remoteOut, r.Method);
            if (string.IsNullOrEmpty(nested))
            {
                nested = ConcatMeshFactoryCode(remoteOut, remoteOfficial, maps);
            }

            if (nested.Length == 0)
            {
                continue;
            }

            AppendNestedBytecode(nested, insertIslandBoundaryBeforeNested: true);
            _ = seenOwners.Add(methodKey);
            _ = seenOwners.Add("pulled:" + own);
        }

        AppendVoidMeshHelperTargets(acc, insertIslandBoundaryBeforeNested: true);

        return acc;
    }
}
