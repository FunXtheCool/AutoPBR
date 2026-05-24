using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>Backward-slice lifter for float expressions ending in <c>ModelPart</c> field stores.</summary>
internal static class SetupAnimExpressionLift
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

    internal static Dictionary<string, JsonObject> BuildFloatLocalBindingsForTest(
        List<string> lines,
        int upToLineExclusive,
        IReadOnlyDictionary<string, float>? modelAccessors) =>
        BuildFloatLocalBindings(lines, upToLineExclusive, modelAccessors);

    private static Dictionary<string, JsonObject> BuildFloatLocalBindings(
        List<string> lines,
        int upToLineExclusive,
        IReadOnlyDictionary<string, float>? modelAccessors)
    {
        var locals = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        for (var i = 0; i < upToLineExclusive; i++)
        {
            if (!TryParseStoreLocal(lines[i], out var localName))
            {
                continue;
            }

            if (TryLiftConditionalFloatStore(lines, i, out var conditional))
            {
                locals[localName] = conditional;
                continue;
            }

            if (TryLiftAssignmentExprFromStoreSimple(lines, i, out var stored, modelAccessors))
            {
                locals[localName] = stored;
            }
        }

        return locals;
    }

    private static bool TryLiftConditionalFloatStore(List<string> lines, int fstoreLineIdx, out JsonObject expr)
    {
        expr = new JsonObject();
        string? stateField = null;
        float? trueBranch = null;
        float? falseBranch = null;

        for (var j = fstoreLineIdx - 1; j >= Math.Max(0, fstoreLineIdx - 14); j--)
        {
            var line = lines[j];
            var stateMatch = RenderStateFieldRegex.Match(line);
            if (stateMatch.Success && line.Contains(":Z", StringComparison.Ordinal))
            {
                stateField = stateMatch.Groups[1].Value;
            }

            if (line.Contains("ldc", StringComparison.Ordinal) && LdcFloatRegex.IsMatch(line))
            {
                var m = LdcFloatRegex.Match(line);
                if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    trueBranch ??= f;
                }
            }

            if (line.Contains("fconst_1", StringComparison.Ordinal))
            {
                falseBranch ??= 1f;
            }

            if (line.Contains("fconst_0", StringComparison.Ordinal))
            {
                falseBranch ??= 0f;
            }
        }

        if (stateField is null || trueBranch is null || falseBranch is null)
        {
            return false;
        }

        expr = new JsonObject
        {
            ["when"] = new JsonObject
            {
                ["state"] = stateField,
                ["cmp"] = "eq",
                ["value"] = true
            },
            ["then"] = ConstNode(trueBranch.Value),
            ["else"] = ConstNode(falseBranch.Value)
        };
        return true;
    }

    private static bool TryLiftAssignmentExprFromStoreSimple(
        List<string> lines,
        int fstoreLineIdx,
        out JsonObject expr,
        IReadOnlyDictionary<string, float>? modelAccessors)
    {
        expr = new JsonObject();
        var stack = new List<StackEntry>();
        for (var i = fstoreLineIdx - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (IsModelPartReferenceLine(line))
            {
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

            if (TryParseStoreLocal(line, out _))
            {
                break;
            }

            if (TryParseLoadLocal(line, out var floadName) &&
                TryResolveLocalFromPriorGetfield(lines, i, floadName, modelAccessors, out var localExpr))
            {
                stack.Add(new StackEntry(CloneExpr(localExpr)));
                continue;
            }

            if (TryLiftUnaryOp(line, stack) || TryLiftBinaryOp(line, stack) || TryLiftStateField(line, stack) ||
                TryLiftConst(line, stack) || TryLiftPartSelfRead(line, stack) ||
                TryLiftModelAccessor(line, stack, modelAccessors) || TryLiftStaticFloatCall(line, stack) ||
                TryLiftLerpCall(line, stack))
            {
                continue;
            }

            if (line.Contains("invokespecial", StringComparison.Ordinal) &&
                line.Contains("setupAnim", StringComparison.Ordinal))
            {
                break;
            }
        }

        if (stack is [{ Node: { } n }])
        {
            expr = n;
            return true;
        }

        return false;
    }

    private static bool TryLiftAssignmentExprFromStore(
        List<string> lines,
        int fstoreLineIdx,
        out JsonObject expr,
        IReadOnlyDictionary<string, float>? modelAccessors) =>
        TryLiftAssignmentExprFromStoreSimple(lines, fstoreLineIdx, out expr, modelAccessors);

    private static bool TryLiftPartSelfRead(string line, List<StackEntry> stack)
    {
        var m = ModelPartPropertyGetRegex.Match(line);
        if (!m.Success)
        {
            return false;
        }

        var prop = m.Groups[1].Value;
        if (prop is not ("xRot" or "yRot" or "zRot" or "x" or "y" or "z"))
        {
            return false;
        }

        stack.Add(new StackEntry(new JsonObject { ["partSelf"] = prop }));
        return true;
    }

    private static bool IsModelPartReferenceLine(string line) =>
        line.Contains("aload_0", StringComparison.Ordinal) ||
        (line.Contains("aload_1", StringComparison.Ordinal) && !line.Contains("getfield", StringComparison.Ordinal)) ||
        line.TrimEnd().EndsWith("dup", StringComparison.Ordinal) ||
        (line.Contains("getfield", StringComparison.Ordinal) &&
         line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal));

    private static bool TryLiftBinaryOp(string line, List<StackEntry> stack)
    {
        if (stack.Count < 2)
        {
            return false;
        }

        string? op = null;
        if (line.Contains(" fadd", StringComparison.Ordinal) || line.EndsWith("fadd", StringComparison.Ordinal))
        {
            op = "add";
        }
        else if (line.Contains(" fmul", StringComparison.Ordinal) || line.EndsWith("fmul", StringComparison.Ordinal))
        {
            op = "mul";
        }
        else if (line.Contains(" fsub", StringComparison.Ordinal) || line.EndsWith("fsub", StringComparison.Ordinal))
        {
            op = "sub";
        }
        else if (line.Contains(" fdiv", StringComparison.Ordinal) || line.EndsWith("fdiv", StringComparison.Ordinal))
        {
            op = "div";
        }

        if (op is null)
        {
            return false;
        }

        var b = stack[^1].Node!;
        var a = stack[^2].Node!;
        stack.RemoveRange(stack.Count - 2, 2);
        stack.Add(new StackEntry(OpNode(op, a, b)));
        return true;
    }

    private static bool TryLiftUnaryOp(string line, List<StackEntry> stack)
    {
        if (stack.Count < 1)
        {
            return false;
        }

        if (line.Contains("Mth.cos", StringComparison.Ordinal) ||
            line.Contains("Math.cos", StringComparison.Ordinal))
        {
            var arg = stack[^1].Node!;
            stack.RemoveAt(stack.Count - 1);
            stack.Add(new StackEntry(OpNode("cos", arg)));
            return true;
        }

        if (line.Contains("Mth.sin", StringComparison.Ordinal) ||
            line.Contains("Math.sin", StringComparison.Ordinal))
        {
            var arg = stack[^1].Node!;
            stack.RemoveAt(stack.Count - 1);
            stack.Add(new StackEntry(OpNode("sin", arg)));
            return true;
        }

        return false;
    }

    private static bool TryLiftStaticFloatCall(string line, List<StackEntry> stack)
    {
        if (stack.Count < 2)
        {
            return false;
        }

        if (!line.Contains("invokestatic", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains("Math.max", StringComparison.Ordinal) ||
            line.Contains("Mth.max", StringComparison.Ordinal))
        {
            var b = stack[^1].Node!;
            var a = stack[^2].Node!;
            stack.RemoveRange(stack.Count - 2, 2);
            stack.Add(new StackEntry(OpNode("max", a, b)));
            return true;
        }

        if (line.Contains("Math.min", StringComparison.Ordinal) ||
            line.Contains("Mth.min", StringComparison.Ordinal))
        {
            var b = stack[^1].Node!;
            var a = stack[^2].Node!;
            stack.RemoveRange(stack.Count - 2, 2);
            stack.Add(new StackEntry(OpNode("min", a, b)));
            return true;
        }

        return false;
    }

    private static bool TryLiftLerpCall(string line, List<StackEntry> stack)
    {
        if (stack.Count < 3)
        {
            return false;
        }

        if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
            (!line.Contains("Math.lerp", StringComparison.Ordinal) &&
             !line.Contains("Mth.lerp", StringComparison.Ordinal) &&
             !line.Contains("rotLerpRad", StringComparison.Ordinal)))
        {
            return false;
        }

        var t = stack[^1].Node!;
        var b = stack[^2].Node!;
        var a = stack[^3].Node!;
        stack.RemoveRange(stack.Count - 3, 3);
        stack.Add(new StackEntry(OpNode("lerp", a, b, t)));
        return true;
    }

    private static bool TryLiftModelAccessor(
        string line,
        List<StackEntry> stack,
        IReadOnlyDictionary<string, float>? modelAccessors)
    {
        if (modelAccessors is null || modelAccessors.Count == 0)
        {
            return false;
        }

        if (!line.Contains("invokevirtual", StringComparison.Ordinal) || !line.Contains(":()F", StringComparison.Ordinal))
        {
            return false;
        }

        var m = Regex.Match(
            line,
            @"Method\s+[\w$/.]+\.(\w+):\(\)F",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (!m.Success || !modelAccessors.TryGetValue(m.Groups[1].Value, out var value))
        {
            return false;
        }

        stack.Add(new StackEntry(ConstNode(value)));
        return true;
    }

    internal static JsonObject PartPeerNode(string peerPartField, string property) =>
        new()
        {
            ["partPeer"] = peerPartField,
            ["peerProperty"] = property
        };

    internal static JsonObject ConstNode(float v) => new() { ["const"] = v };

    internal static JsonObject StateNode(string field) => new() { ["state"] = field };

    internal static JsonObject OpNode(string op, params JsonObject[] args)
    {
        var arr = new JsonArray();
        foreach (var a in args)
        {
            arr.Add(CloneExpr(a));
        }

        return new JsonObject
        {
            ["op"] = op,
            ["args"] = arr
        };
    }

    internal static JsonObject CloneExpr(JsonObject o) => JsonNode.Parse(o.ToJsonString())!.AsObject();

    private static bool TryResolveLocalFromPriorGetfield(
          List<string> lines,
          int floadLineIdx,
          string localName,
          IReadOnlyDictionary<string, float>? modelAccessors,
          out JsonObject expr)
    {
        expr = new JsonObject();
        if (!IsFLocalName(localName))
        {
            return false;
        }

        for (var i = floadLineIdx - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (TryParseStoreLocal(line, out var storeName) && storeName == localName)
            {
                if (TryLiftConditionalFloatStore(lines, i, out var conditional))
                {
                    expr = conditional;
                    return true;
                }

                if (TryLiftAssignmentExprFromStoreSimple(lines, i, out var stored, modelAccessors) && stored.Count > 0)
                {
                    expr = stored;
                    return true;
                }

                for (var j = i - 1; j >= Math.Max(0, i - 8); j--)
                {
                    var m = RenderStateFieldRegex.Match(lines[j]);
                    if (m.Success)
                    {
                        expr = StateNode(m.Groups[1].Value);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsFLocalName(string localName) =>
        Regex.IsMatch(localName, @"^f\d+$", RegexOptions.None, TimeSpan.FromSeconds(1));
}
