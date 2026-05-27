using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimExpressionLift
{
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

}
