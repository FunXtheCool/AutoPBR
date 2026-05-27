using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>Backward-slice lifter for float expressions ending in <c>ModelPart</c> field stores.</summary>
internal static partial class SetupAnimExpressionLift
{
    private static readonly Regex LdcFloatRegex = new(
        @"//\s*float\s+(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)f",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private static readonly Regex RenderStateFieldRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+net/minecraft/client/renderer/entity/state/[\w$.]+\.(\w+):",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex ModelStateFloatFieldRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+[\w$./]+\.(\w+):F",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex ModelPartPropertyGetRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+net/minecraft/client/model/geom/ModelPart\.(\w+):F",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private sealed record StackEntry(JsonObject? Node);

    public static bool TryLiftAssignmentExpr(
        List<string> lines,
        int putfieldLineIdx,
        out JsonObject? expr,
        out List<string> notes,
        IReadOnlyDictionary<string, float>? modelAccessors = null)
    {
        expr = null;
        notes = [];
        var locals = BuildFloatLocalBindings(lines, lines.Count, modelAccessors);
        var windowStart = FindAssignmentWindowStart(lines, putfieldLineIdx);
        var stack = new List<StackEntry>();

        for (var i = windowStart; i < putfieldLineIdx; i++)
        {
            var line = lines[i];
            if (ShouldSkipSetupAnimExprLine(line) || IsModelPartReferenceLine(line))
            {
                continue;
            }

            if (TryParseStoreLocal(line, out var localName))
            {
                if (stack.Count > 0 && stack[^1].Node is { } n)
                {
                    locals[localName] = n;
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            if (TryParseLoadLocal(line, out var floadName) && locals.TryGetValue(floadName, out var localExpr))
            {
                stack.Add(new StackEntry(CloneExpr(localExpr)));
                continue;
            }

            if (line.Contains("f2d", StringComparison.Ordinal) ||
                line.Contains("d2f", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.TrimEnd().EndsWith("dup", StringComparison.Ordinal))
            {
                if (stack.Count > 0 && stack[^1].Node is { } dupNode)
                {
                    stack.Add(new StackEntry(CloneExpr(dupNode)));
                }

                continue;
            }

            if (TryLiftUnaryOp(line, stack) || TryLiftBinaryOp(line, stack) || TryLiftStateField(line, stack) ||
                TryLiftConst(line, stack) || TryLiftPartSelfRead(line, stack) ||
                TryLiftModelAccessor(line, stack, modelAccessors) || TryLiftStaticFloatCall(line, stack) ||
                TryLiftLerpCall(line, stack))
            {
                continue;
            }

            if (TryParseLoadLocal(line, out var unresolvedLoad))
            {
                if (TryResolveLocalFromPriorGetfield(lines, i, unresolvedLoad, modelAccessors, out var fromField))
                {
                    stack.Add(new StackEntry(CloneExpr(fromField)));
                    continue;
                }

                notes.Add($"Unsupported fload at line {i} before putfield.");
                return false;
            }
        }

        if (stack.Count != 1 || stack[0].Node is not { } result)
        {
            notes.Add($"Expression stack did not reduce to one node (count={stack.Count}).");
            return false;
        }

        expr = result;
        return true;
    }

    private static int FindAssignmentWindowStart(List<string> lines, int putfieldLineIdx)
    {
        var targetPart = FindTargetPartFieldForPutfield(lines, putfieldLineIdx);
        if (!string.IsNullOrEmpty(targetPart))
        {
            for (var i = putfieldLineIdx - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Contains($"Field {targetPart}:", StringComparison.Ordinal) &&
                    line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
                {
                    return i;
                }

                if (line.Contains("invokespecial", StringComparison.Ordinal) &&
                    line.Contains("setupAnim", StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
        }

        for (var i = putfieldLineIdx - 1; i >= 0; i--)
        {
            if (lines[i].Contains("putfield", StringComparison.Ordinal) &&
                lines[i].Contains("ModelPart.", StringComparison.Ordinal))
            {
                return i + 1;
            }

            if (lines[i].Contains("invokespecial", StringComparison.Ordinal) &&
                lines[i].Contains("setupAnim", StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static string? FindTargetPartFieldForPutfield(List<string> lines, int putIdx)
    {
        for (var j = putIdx - 1; j >= Math.Max(0, putIdx - 40); j--)
        {
            var line = lines[j];
            if (line.Contains("getfield", StringComparison.Ordinal) &&
                line.Contains("net/minecraft/client/model/geom/ModelPart.", StringComparison.Ordinal) &&
                !line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.Contains("getfield", StringComparison.Ordinal) ||
                !line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
            {
                continue;
            }

            var m = Regex.Match(
                line,
                @"Field\s+(\w+):Lnet/minecraft/client/model/geom/ModelPart;",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return null;
    }

    private static bool TryParseStoreLocal(string line, out string name)
    {
        name = "";
        var m = Regex.Match(line, @"\bfstore_(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!m.Success)
        {
            m = Regex.Match(line, @"\bfstore\s+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        if (m.Success)
        {
            name = $"f{m.Groups[1].Value}";
            return true;
        }

        return false;
    }

    private static bool TryParseLoadLocal(string line, out string name)
    {
        name = "";
        var m = Regex.Match(line, @"\bfload_(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!m.Success)
        {
            m = Regex.Match(line, @"\bfload\s+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        if (m.Success)
        {
            name = $"f{m.Groups[1].Value}";
            return true;
        }

        return false;
    }

    private static bool TryLiftConst(string line, List<StackEntry> stack)
    {
        var m = LdcFloatRegex.Match(line);
        if (m.Success &&
            float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
        {
            stack.Add(new StackEntry(ConstNode(f)));
            return true;
        }

        if (line.Contains("fconst_0", StringComparison.Ordinal))
        {
            stack.Add(new StackEntry(ConstNode(0f)));
            return true;
        }

        if (line.Contains("fconst_1", StringComparison.Ordinal))
        {
            stack.Add(new StackEntry(ConstNode(1f)));
            return true;
        }

        if (line.Contains("fconst_2", StringComparison.Ordinal))
        {
            stack.Add(new StackEntry(ConstNode(2f)));
            return true;
        }

        if (line.Contains("ldc", StringComparison.Ordinal) && line.Contains("3.141592", StringComparison.Ordinal))
        {
            stack.Add(new StackEntry(ConstNode(MathF.PI)));
            return true;
        }

        return false;
    }

    private static bool TryLiftStateField(string line, List<StackEntry> stack)
    {
        if (line.Contains(":Z", StringComparison.Ordinal) ||
            line.Contains(":I", StringComparison.Ordinal) ||
            line.Contains(":J", StringComparison.Ordinal))
        {
            return false;
        }

        var m = RenderStateFieldRegex.Match(line);
        if (!m.Success)
        {
            m = ModelStateFloatFieldRegex.Match(line);
        }

        if (!m.Success || line.Contains("ModelPart.", StringComparison.Ordinal))
        {
            return false;
        }

        stack.Add(new StackEntry(StateNode(m.Groups[1].Value)));
        return true;
    }

    private static bool ShouldSkipSetupAnimExprLine(string line) =>
        line.Contains("ifeq", StringComparison.Ordinal) ||
        line.Contains("ifne", StringComparison.Ordinal) ||
        line.Contains("if_icmpeq", StringComparison.Ordinal) ||
        line.Contains("if_icmpne", StringComparison.Ordinal) ||
        line.Contains(" goto ", StringComparison.Ordinal) ||
        line.TrimEnd().EndsWith("goto", StringComparison.Ordinal) ||
        (line.Contains("aload_1", StringComparison.Ordinal) && !line.Contains("getfield", StringComparison.Ordinal));
}
