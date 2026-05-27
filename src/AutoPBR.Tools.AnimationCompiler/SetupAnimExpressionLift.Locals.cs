using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimExpressionLift
{
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

}
