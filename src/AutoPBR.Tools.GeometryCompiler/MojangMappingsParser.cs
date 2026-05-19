using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Parses Mojang ProGuard <c>client_mappings.txt</c> / <c>client.txt</c> lines: <c>named.full.Type -> obfRhs:</c>
/// plus indented <c>… ReturnType method(args) -> obfName</c> rows for mesh factory resolution on obfuscated jars.
/// </summary>
internal sealed class MojangMappingsParser
{
    private static readonly Regex MethodLineRegex = new(
        @"^\s*\d+:\d+:\s*(.+)\s+(\w+)\s*\(([^)]*)\)\s*->\s*(\w+)\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private readonly Dictionary<string, string> _namedToObf;
    private readonly Dictionary<string, List<MappedMethodRow>> _methodsByNamedOuter;
    private readonly Dictionary<string, string> _obfJarSimpleToNamedOuter;
    private readonly Dictionary<string, Dictionary<string, string>> _namedFieldByObfByOuter;

    private MojangMappingsParser(Dictionary<string, string> namedToObf,
        Dictionary<string, List<MappedMethodRow>> methodsByNamedOuter,
        Dictionary<string, string> obfJarSimpleToNamedOuter,
        Dictionary<string, Dictionary<string, string>> namedFieldByObfByOuter)
    {
        _namedToObf = namedToObf;
        _methodsByNamedOuter = methodsByNamedOuter;
        _obfJarSimpleToNamedOuter = obfJarSimpleToNamedOuter;
        _namedFieldByObfByOuter = namedFieldByObfByOuter;
    }

    public static MojangMappingsParser Load(string path)
    {
        var namedToObf = new Dictionary<string, string>(StringComparer.Ordinal);
        var methodsByOuter = new Dictionary<string, List<MappedMethodRow>>(StringComparer.Ordinal);
        var fieldsByOuter = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        string? currentOuterNamed = null;

        foreach (var raw in File.ReadAllLines(path))
        {
            if (raw.Length == 0 || raw.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (!char.IsWhiteSpace(raw[0]))
            {
                var tr = raw.Trim();
                const string arrow = " -> ";
                var ai = tr.IndexOf(arrow, StringComparison.Ordinal);
                if (ai < 0 || !tr.EndsWith(':'))
                {
                    currentOuterNamed = null;
                    continue;
                }

                var named = tr[..ai].Trim();
                var obfRhs = tr[(ai + arrow.Length)..^1].Trim();
                if (named.Length >= 4 && char.IsDigit(named[0]) && named.Contains(':', StringComparison.Ordinal))
                {
                    currentOuterNamed = null;
                    continue;
                }

                var obfFqn = ResolveObfuscatedClassName(named, obfRhs);
                namedToObf[named] = obfFqn;
                currentOuterNamed = GetOuterClassFqn(named);
                continue;
            }

            if (currentOuterNamed is null)
            {
                continue;
            }

            var t = raw.Trim();
            if (!t.Contains(" -> ", StringComparison.Ordinal))
            {
                continue;
            }

            var arrowIdx = t.LastIndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIdx > 0 && !t.Contains('(', StringComparison.Ordinal))
            {
                var fieldLeft = t[..arrowIdx].Trim();
                var obfField = t[(arrowIdx + " -> ".Length)..].Trim();
                if (obfField.Length > 0 && !obfField.Contains(' ', StringComparison.Ordinal) &&
                    TryParseFieldMappingLeft(fieldLeft, out var namedField))
                {
                    if (!fieldsByOuter.TryGetValue(currentOuterNamed, out var fieldMap))
                    {
                        fieldMap = new Dictionary<string, string>(StringComparer.Ordinal);
                        fieldsByOuter[currentOuterNamed] = fieldMap;
                    }

                    fieldMap[obfField] = namedField;
                }

                continue;
            }

            arrowIdx = t.LastIndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIdx < 0)
            {
                continue;
            }

            var left = t[..arrowIdx].Trim();
            var obfMethod = t[(arrowIdx + " -> ".Length)..].Trim();
            if (obfMethod.Length == 0 || obfMethod.Contains(' ', StringComparison.Ordinal) ||
                obfMethod.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            if (!left.Contains('(', StringComparison.Ordinal))
            {
                continue;
            }

            var m = MethodLineRegex.Match(t);
            if (!m.Success)
            {
                continue;
            }

            var namedReturn = m.Groups[1].Value.Trim();
            var namedMethod = m.Groups[2].Value;
            var namedArgs = m.Groups[3].Value.Trim();
            if (namedMethod.StartsWith('<'))
            {
                continue;
            }

            if (!methodsByOuter.TryGetValue(currentOuterNamed, out var list))
            {
                list = new List<MappedMethodRow>();
                methodsByOuter[currentOuterNamed] = list;
            }

            list.Add(new MappedMethodRow(namedReturn, namedMethod, namedArgs, obfMethod));
        }

        var inverse = BuildObfSimpleToNamedOuter(namedToObf);
        return new MojangMappingsParser(namedToObf, methodsByOuter, inverse, fieldsByOuter);
    }

    private static bool TryParseFieldMappingLeft(string left, out string namedField)
    {
        namedField = string.Empty;
        var lastSpace = left.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace >= left.Length - 1)
        {
            return false;
        }

        namedField = left[(lastSpace + 1)..].Trim();
        return namedField.Length > 0 && char.IsLetter(namedField[0]);
    }

    /// <summary>Maps an obfuscated static field on <paramref name="namedOuter"/> to its Mojang name.</summary>
    public bool TryGetNamedField(string namedOuter, string obfuscatedField, out string namedField)
    {
        namedField = string.Empty;
        return _namedFieldByObfByOuter.TryGetValue(namedOuter, out var map) &&
               map.TryGetValue(obfuscatedField, out namedField!);
    }

    public IReadOnlyDictionary<string, string> GetObfToNamedFields(string namedOuter) =>
        _namedFieldByObfByOuter.TryGetValue(namedOuter, out var map)
            ? map
            : new Dictionary<string, string>(StringComparer.Ordinal);

    private static Dictionary<string, string> BuildObfSimpleToNamedOuter(Dictionary<string, string> namedToObf)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in namedToObf)
        {
            var namedOuter = GetOuterClassFqn(kv.Key);
            var simple = GetJavapClassArgForObfuscated(kv.Value);
            d[simple] = namedOuter;
        }

        return d;
    }

    public bool TryGetObfuscated(string namedFqn, out string obfuscatedFqn) =>
        _namedToObf.TryGetValue(namedFqn, out obfuscatedFqn!);

    /// <summary>Resolve a javap outer simple name (e.g. <c>hap</c>) to the official outer class FQN.</summary>
    public bool TryGetNamedOuterFromJarSimple(string jarSimpleOuter, out string namedOuter)
    {
        if (_obfJarSimpleToNamedOuter.TryGetValue(jarSimpleOuter, out namedOuter!))
        {
            return true;
        }

        namedOuter = string.Empty;
        return false;
    }

    private const string PartPoseOfficialOuter = "net.minecraft.client.model.geom.PartPose";

    /// <summary>Maps obfuscated <c>PartPose</c> factory method + javap arg list to Mojang name (e.g. <c>offset</c>).</summary>
    public bool TryResolveObfuscatedPartPoseFactory(string obfPoseShort, string obfMethod, string javapArgsInParens,
        out string namedMethod)
    {
        namedMethod = string.Empty;
        if (!TryGetNamedOuterFromJarSimple(obfPoseShort, out var outer) ||
            !outer.Contains("PartPose", StringComparison.Ordinal))
        {
            return false;
        }

        if (!_methodsByNamedOuter.TryGetValue(outer, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (!string.Equals(row.ObfuscatedMethod, obfMethod, StringComparison.Ordinal))
            {
                continue;
            }

            var mappedArgs = MapNamedParameterListToJavapSource(row.NamedArgs);
            if (!string.Equals(mappedArgs, javapArgsInParens, StringComparison.Ordinal))
            {
                continue;
            }

            namedMethod = row.NamedMethod;
            return true;
        }

        return false;
    }

    public bool TryGetObfuscatedPartPoseShort(out string obfShort)
    {
        obfShort = string.Empty;
        if (!TryGetObfuscated(PartPoseOfficialOuter, out var obfFqn))
        {
            return false;
        }

        obfShort = GetJavapClassArgForObfuscated(obfFqn);
        return true;
    }

    /// <summary>Returns true when <paramref name="javapReturnTypeShort"/> maps to an official type whose name contains <paramref name="officialTypeFragment"/>.</summary>
    public bool TryIsObfuscatedReturnType(string javapReturnTypeShort, string officialTypeFragment)
    {
        if (!TryGetNamedOuterFromJarSimple(javapReturnTypeShort.TrimStart('L').TrimEnd(';'), out var namedOuter))
        {
            return false;
        }

        if (!_methodsByNamedOuter.TryGetValue(namedOuter, out var rows))
        {
            return namedOuter.Contains(officialTypeFragment, StringComparison.Ordinal);
        }

        return rows.Exists(r => r.NamedReturnType.Contains(officialTypeFragment, StringComparison.Ordinal));
    }

    /// <summary>
    /// Mesh-related static factories on <paramref name="namedOuterClass"/> for <c>javap -c</c> slicing:
    /// every <c>createBodyLayer</c> overload, methods whose named return type contains <c>MeshDefinition</c>,
    /// then methods whose named return type contains <c>LayerDefinition</c> (e.g. <c>createCapeLayer</c>, <c>createLayer</c>).
    /// </summary>
    public IEnumerable<MeshFactoryPin> EnumerateMeshFactoryPins(string namedOuterClass)
    {
        if (!_methodsByNamedOuter.TryGetValue(namedOuterClass, out var rows))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pin in OrderMeshPins(rows))
        {
            var key = pin.ObfuscatedMethod + "\0" + pin.JavapParameterList;
            if (seen.Add(key))
            {
                yield return pin;
            }
        }
    }

    private IEnumerable<MeshFactoryPin> OrderMeshPins(List<MappedMethodRow> rows)
    {
        static int CreateBodyLayerRank(MappedMethodRow r)
        {
            if (!string.Equals(r.NamedMethod, "createBodyLayer", StringComparison.Ordinal))
            {
                return 1000;
            }

            if (r.NamedArgs.Length == 0)
            {
                return 0;
            }

            if (string.Equals(r.NamedArgs, "float", StringComparison.Ordinal))
            {
                return 1;
            }

            if (r.NamedArgs.Contains("CubeDeformation", StringComparison.Ordinal))
            {
                return 2;
            }

            return 3;
        }

        foreach (var r in rows.Where(x => string.Equals(x.NamedMethod, "createBodyLayer", StringComparison.Ordinal))
                     .OrderBy(CreateBodyLayerRank)
                     .ThenBy(r => r.NamedArgs.Length))
        {
            yield return ToPin(r);
        }

        foreach (var r in rows)
        {
            if (string.Equals(r.NamedMethod, "createBodyLayer", StringComparison.Ordinal))
            {
                continue;
            }

            if (!r.NamedReturnType.Contains("MeshDefinition", StringComparison.Ordinal) &&
                !r.NamedReturnType.Contains("LayerDefinition", StringComparison.Ordinal))
            {
                continue;
            }

            yield return ToPin(r);
        }
    }

    private MeshFactoryPin ToPin(MappedMethodRow r)
    {
        var javapArgs = MapNamedParameterListToJavapSource(r.NamedArgs);
        return new MeshFactoryPin(r.ObfuscatedMethod, javapArgs, r.NamedMethod, r.NamedReturnType);
    }

    /// <summary>Maps <c>float,net.minecraft…Foo</c> to <c>float,Lhxx;</c> for matching <c>javap -public</c> declarations.</summary>
    public string MapNamedParameterListToJavapSource(string namedCommaSeparatedArgs)
    {
        if (string.IsNullOrWhiteSpace(namedCommaSeparatedArgs))
        {
            return string.Empty;
        }

        var parts = namedCommaSeparatedArgs.Split(',');
        var mapped = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length == 0)
            {
                continue;
            }

            mapped.Add(MapOneNamedParameterToJavap(t));
        }

        return string.Join(",", mapped);
    }

    private string MapOneNamedParameterToJavap(string namedType)
    {
        if (IsPrimitiveJvmName(namedType))
        {
            return namedType;
        }

        if (!TryGetObfuscated(namedType, out var obf))
        {
            return namedType;
        }

        return "L" + GetJavapClassArgForObfuscated(obf) + ";";
    }

    private static bool IsPrimitiveJvmName(string t) =>
        t is "void" or "boolean" or "byte" or "char" or "short" or "int" or "long" or "float" or "double";

    /// <summary>Javap classpath argument for obfuscated jars: outer simple binary name (e.g. <c>gbs</c>).</summary>
    public static string GetJavapClassArgForObfuscated(string obfuscatedFqn)
    {
        var last = obfuscatedFqn.LastIndexOf('.');
        return last < 0 ? obfuscatedFqn : obfuscatedFqn[(last + 1)..];
    }

    private static string GetOuterClassFqn(string namedClass)
    {
        var d = namedClass.IndexOf('$');
        return d < 0 ? namedClass : namedClass[..d];
    }

    private static string ResolveObfuscatedClassName(string named, string obfRhs)
    {
        if (obfRhs.Contains('.', StringComparison.Ordinal))
        {
            return obfRhs;
        }

        var outer = GetOuterClassFqn(named);
        var idx = outer.LastIndexOf('.');
        if (idx <= 0)
        {
            return obfRhs;
        }

        var pkg = outer[..idx];
        return $"{pkg}.{obfRhs}";
    }

    private readonly record struct MappedMethodRow(string NamedReturnType, string NamedMethod, string NamedArgs,
        string ObfuscatedMethod);

    internal readonly record struct MeshFactoryPin(string ObfuscatedMethod, string JavapParameterList, string NamedMethod,
        string NamedReturnType);
}
