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
}
