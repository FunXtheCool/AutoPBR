using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Runs <c>javap -c</c> for a single class and exposes helpers to slice method bodies from stdout.
/// </summary>
internal static class JavapClassDisassembly
{
    private static readonly ConcurrentDictionary<string, Lazy<CachedDisasm>> DisasmCache = new(StringComparer.Ordinal);

    private sealed record CachedDisasm(bool Ok, string Stdout, string? Error);

    internal static void ClearDisassemblyCacheForTests() => DisasmCache.Clear();

    /// <summary>
    /// Synthetic line inserted between bytecode chunks merged from different classes (e.g. host <c>createBodyLayer</c> +
    /// <c>PlayerModel.createMesh</c> + <c>AbstractPiglinModel.addHead</c>) and between multiple static mesh <c>Code:</c> blocks from the
    /// same <c>javap</c> class (see <see cref="ConcatMeshFactoryCodeNamed"/> / <see cref="ConcatMeshFactoryCode"/>). Without it,
    /// <see cref="JavapFloatGeometryMeshLift"/> would reuse local-slot maps across unrelated methods and mis-nest parts.
    /// </summary>
    internal const string GeometryMeshIslandBoundaryMarker = "// __AUTOPBR_GEOMETRY_MESH_ISLAND__";

    private static readonly Regex StaticMeshDefinitionMethodDeclRegex = new(
        @"^\s+.*\bstatic\b.*\bMeshDefinition\s+(\w+)\s*\([^)]*\)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    /// <summary>Static factories returning <c>LayerDefinition</c> (cape/ears layers, effect models, etc.).</summary>
    private static readonly Regex StaticLayerDefinitionMethodDeclRegex = new(
        @"^\s+.*\bstatic\b.*\bLayerDefinition\s+(\w+)\s*\([^)]*\)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticReturnsMeshDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L[\w/$]+MeshDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticSameClassMeshDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w$]+):\([^)]*\)L[\w/$]+MeshDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticReturnsLayerDefinitionCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L[\w/$]+LayerDefinition;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex InvokeStaticObfuscatedReturnCommentRegex = new(
        @"//\s*Method\s+([\w\./]+)\.([\w$]+):\([^)]*\)L(\w+);\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    public static bool TryDisassemble(string? javapExe, string clientJar, string javapClassArg, out string stdout,
        out string? error)
    {
        var key = BuildDisasmCacheKey(javapExe, clientJar, javapClassArg);
        if (DisasmCache.TryGetValue(key, out var existingLazy))
        {
            GeometryCompilerStats.NoteDisasmCacheHit();
            return UnpackCached(existingLazy.Value, out stdout, out error);
        }

        var lazy = DisasmCache.GetOrAdd(key, _ => new Lazy<CachedDisasm>(
            () => RunDisassembleUncached(javapExe, clientJar, javapClassArg),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return UnpackCached(lazy.Value, out stdout, out error);
    }

    private static bool UnpackCached(CachedDisasm c, out string stdout, out string? error)
    {
        stdout = c.Stdout;
        error = c.Error;
        return c.Ok;
    }

    private static string BuildDisasmCacheKey(string? javapExe, string clientJar, string javapClassArg)
    {
        string normExe;
        try
        {
            normExe = string.IsNullOrWhiteSpace(javapExe) ? "" : Path.GetFullPath(javapExe);
        }
        catch
        {
            normExe = javapExe ?? "";
        }

        string normJar;
        try
        {
            normJar = Path.GetFullPath(clientJar);
        }
        catch
        {
            normJar = clientJar;
        }

        return normExe + '\u241E' + normJar + '\u241E' + javapClassArg;
    }

    private static string ResolveJavapExecutable(string? javapExe) =>
        string.IsNullOrWhiteSpace(javapExe) ? JavapLocator.FindJavap() ?? "javap" : javapExe;

    private static CachedDisasm RunDisassembleUncached(string? javapExe, string clientJar, string javapClassArg)
    {
        GeometryCompilerStats.NoteJavapSubprocess();
        javapExe = ResolveJavapExecutable(javapExe);
        if (string.IsNullOrWhiteSpace(javapExe))
        {
            return new CachedDisasm(false, string.Empty, "javap executable not found (set JAVA_HOME or PATH).");
        }

        if (!File.Exists(clientJar))
        {
            return new CachedDisasm(false, string.Empty, $"client.jar not found: {clientJar}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = javapExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-encoding");
        psi.ArgumentList.Add("UTF8");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("-classpath");
        psi.ArgumentList.Add(clientJar);
        psi.ArgumentList.Add(javapClassArg);

        using var p = Process.Start(psi);
        if (p is null)
        {
            return new CachedDisasm(false, string.Empty, "Failed to start javap.");
        }

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            return new CachedDisasm(false, stdout, $"javap exit {p.ExitCode}: {stderr}{stdout}");
        }

        return new CachedDisasm(true, stdout, null);
    }

    /// <summary>Extracts the <c>Code:</c> block for <c>methodName(</c> … first overload in disassembly.</summary>
    public static string? ExtractMethodCodeBlock(string javapC, string methodName)
    {
        var needle = methodName + "(";
        var idx = javapC.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        return ExtractCodeBlockAfterDeclarationIndex(javapC, idx);
    }

    /// <summary>Extracts <c>Code:</c> after a <c>javap -public</c> declaration containing <paramref name="signatureNeedle"/> (e.g. <c> e();</c>).</summary>
    public static string? ExtractMethodCodeBlockBySignatureNeedle(string javapC, string signatureNeedle)
    {
        foreach (var needle in EnumerateJavapSignatureNeedleVariants(signatureNeedle))
        {
            var idx = javapC.IndexOf(needle, StringComparison.Ordinal);
            if (idx >= 0)
            {
                return ExtractCodeBlockAfterDeclarationIndex(javapC, idx);
            }
        }

        return null;
    }

    /// <summary>
    /// <c>javap</c> bytecode comments use JVM descriptors (<c>Lnet/minecraft/…;</c>) while declaration lines use Java source types
    /// (<c>net.minecraft…</c>, comma-separated). Try both so <see cref="EnumerateInvokeStaticMeshRefs"/> needles resolve.
    /// </summary>
    internal static IEnumerable<string> EnumerateJavapSignatureNeedleVariants(string signatureNeedle)
    {
        if (signatureNeedle.Length == 0)
        {
            yield break;
        }

        yield return signatureNeedle;
        var alt = TryConvertInvokeCommentDescriptorNeedleToSourceDeclaration(signatureNeedle);
        if (alt is not null && !string.Equals(alt, signatureNeedle, StringComparison.Ordinal))
        {
            yield return alt;
        }
    }

    private static string? TryConvertInvokeCommentDescriptorNeedleToSourceDeclaration(string needle)
    {
        var open = needle.IndexOf('(');
        if (open < 0)
        {
            return null;
        }

        var close = needle.LastIndexOf(')');
        if (close <= open)
        {
            return null;
        }

        var inner = needle.AsSpan(open + 1, close - open - 1);
        if (inner.IndexOf('L') < 0 || inner.IndexOf('/') < 0 || inner.IndexOf('[') >= 0)
        {
            return null;
        }

        if (!TrySplitJvmMethodParameterDescriptors(inner, out var types) || types.Count == 0)
        {
            return null;
        }

        var prefix = needle[..(open + 1)];
        var suffix = needle[close..];
        return prefix + string.Join(", ", types) + suffix;
    }

    private static bool TrySplitJvmMethodParameterDescriptors(ReadOnlySpan<char> inner, out List<string> types)
    {
        types = new List<string>();
        for (var i = 0; i < inner.Length;)
        {
            var c = inner[i];
            if (c == 'L')
            {
                var end = inner[i..].IndexOf(';');
                if (end < 0)
                {
                    types.Clear();
                    return false;
                }

                var endAbs = i + end;
                types.Add(inner[(i + 1)..endAbs].ToString().Replace('/', '.'));
                i = endAbs + 1;
                continue;
            }

            if (c is 'B' or 'C' or 'D' or 'F' or 'I' or 'J' or 'S' or 'Z' or 'V')
            {
                types.Add(c.ToString());
                i++;
                continue;
            }

            types.Clear();
            return false;
        }

        return types.Count > 0;
    }

    private static string? ExtractCodeBlockAfterDeclarationIndex(string javapC, int declarationIdx)
    {
        var codeMark = javapC.IndexOf("    Code:", declarationIdx, StringComparison.Ordinal);
        if (codeMark < 0)
        {
            return null;
        }

        var after = codeMark + "    Code:".Length;
        var end = FindNextMethodDeclarationIndex(javapC, after);
        if (end < 0)
        {
            end = javapC.IndexOf("\n    }", after, StringComparison.Ordinal);
        }

        return end < 0 ? javapC[codeMark..] : javapC[codeMark..end];
    }

    private static int FindNextMethodDeclarationIndex(string javapC, int searchFrom)
    {
        var best = -1;
        foreach (var prefix in new[]
                 {
                     "\n  public ", "\n  protected ", "\n  private ", "\n  static "
                 })
        {
            var i = javapC.IndexOf(prefix, searchFrom, StringComparison.Ordinal);
            if (i >= 0 && (best < 0 || i < best))
            {
                best = i;
            }
        }

        return best;
    }

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
