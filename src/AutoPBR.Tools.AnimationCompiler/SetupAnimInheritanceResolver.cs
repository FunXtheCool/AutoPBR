using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>
/// Resolves <c>inheritsSetupAnimFrom</c> when a model class has no own <c>setupAnim</c> bytecode.
/// </summary>
internal static class SetupAnimInheritanceResolver
{
    private static readonly Regex ExtendsRegex = new(
        @"public\s+(?:abstract\s+)?class\s+[\w$.]+(?:<[^>]*>)?\s+extends\s+([\w$./]+)",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static bool TryResolveSetupAnimHost(
        string javapExe,
        string clientJar,
        string officialJvmName,
        string leafDisasm,
        out string hostJvmName,
        out string hostDisasm,
        out string? immediateSuperJvm)
    {
        hostJvmName = "";
        hostDisasm = "";
        immediateSuperJvm = TryParseImmediateSuper(leafDisasm);
        foreach (var type in EnumerateSuperTypes(javapExe, clientJar, officialJvmName, leafDisasm))
        {
            if (!ClientJarClassBytes.TryReadClass(clientJar, type, out _))
            {
                continue;
            }

            if (!JavapRunner.TryDisassemble(javapExe, clientJar, type, out var typeDisasm, out _))
            {
                continue;
            }

            if (!SetupAnimLift.TryLift(typeDisasm, type, out var lifted, out _))
            {
                continue;
            }

            var hasRules = lifted["assignments"] is JsonArray { Count: > 0 } ||
                           lifted["playbackSteps"] is JsonArray { Count: > 0 } ||
                           lifted["inheritsSetupAnimFrom"] is JsonValue ||
                           lifted["setupAnimEffectOnly"] is JsonValue;
            if (!hasRules)
            {
                continue;
            }

            if (string.Equals(type, officialJvmName, StringComparison.Ordinal))
            {
                continue;
            }

            hostJvmName = type;
            hostDisasm = typeDisasm;
            return true;
        }

        return false;
    }

    public static string? TryParseImmediateSuper(string javapStdout)
    {
        foreach (var line in javapStdout.Split('\n'))
        {
            var m = ExtendsRegex.Match(line);
            if (!m.Success)
            {
                continue;
            }

            return NormalizeTypeName(m.Groups[1].Value);
        }

        return null;
    }

    public static IEnumerable<string> EnumerateSuperTypes(
        string javapExe,
        string clientJar,
        string officialJvmName,
        string? leafDisasm = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = officialJvmName;
        for (var depth = 0; depth < 12; depth++)
        {
            if (!seen.Add(current))
            {
                yield break;
            }

            yield return current;

            var disasm = depth == 0 ? leafDisasm : null;
            if (disasm is null)
            {
                if (!JavapRunner.TryDisassemble(javapExe, clientJar, current, out disasm, out _))
                {
                    yield break;
                }
            }

            var super = TryParseImmediateSuper(disasm);
            if (string.IsNullOrEmpty(super))
            {
                yield break;
            }

            current = super;
        }
    }

    private static string NormalizeTypeName(string raw)
    {
        var s = raw.Replace('/', '.');
        var lt = s.IndexOf('<');
        if (lt > 0)
        {
            s = s[..lt];
        }

        return s.Replace('$', '.');
    }
}
