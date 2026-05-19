using System.Text.Json.Nodes;

using JetBrains.Annotations;

namespace AutoPBR.Core.Preview;

/// <summary>Evaluates lifted setupAnim expression AST against a render-state field bag.</summary>
internal static class SetupAnimExprEvaluator
{
    public static float Evaluate(JsonNode? expr, IReadOnlyDictionary<string, float> state)
    {
        if (expr is null)
        {
            return 0f;
        }

        if (expr is JsonObject o)
        {
            if (o.TryGetPropertyValue("const", out var c) && c is JsonValue cv)
            {
                return (float)cv.GetValue<double>();
            }

            if (o.TryGetPropertyValue("state", out var s) && s is JsonValue sv)
            {
                var key = sv.GetValue<string>();
                if (key.EndsWith(".isStarted", StringComparison.Ordinal))
                {
                    return state.TryGetValue(key, out var b) && b >= 0.5f ? 1f : 0f;
                }

                return state.GetValueOrDefault(key, 0f);
            }

            if (o.TryGetPropertyValue("when", out var whenNode) && whenNode is JsonObject when)
            {
                var pass = EvaluateCondition(when, state);
                var branch = pass
                    ? o["then"]
                    : o["else"];
                return Evaluate(branch, state);
            }

            if (o.TryGetPropertyValue("op", out var opNode) && opNode is JsonValue opv &&
                o["args"] is JsonArray args)
            {
                var op = opv.GetValue<string>();
                return op switch
                {
                    "add" => Evaluate(args[0], state) + Evaluate(args[1], state),
                    "sub" => Evaluate(args[0], state) - Evaluate(args[1], state),
                    "mul" => Evaluate(args[0], state) * Evaluate(args[1], state),
                    "div" => Evaluate(args[0], state) / Evaluate(args[1], state),
                    "neg" => -Evaluate(args[0], state),
                    "sin" => MathF.Sin(Evaluate(args[0], state)),
                    "cos" => MathF.Cos(Evaluate(args[0], state)),
                    _ => 0f
                };
            }
        }

        return 0f;
    }

    [UsedImplicitly(ImplicitUseKindFlags.Access)]
    public static bool EvaluateBool(JsonNode? expr, IReadOnlyDictionary<string, float> state)
    {
        var v = Evaluate(expr, state);
        return v >= 0.5f;
    }

    private static bool EvaluateCondition(JsonObject when, IReadOnlyDictionary<string, float> state)
    {
        var field = (string?)when["state"] ?? "";
        var cmp = (string?)when["cmp"] ?? "eq";
        if (!state.TryGetValue(field, out var actual))
        {
            return false;
        }

        if (when["value"] is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b))
            {
                var ab = actual >= 0.5f;
                return cmp switch
                {
                    "eq" => ab == b,
                    "ne" => ab != b,
                    _ => false
                };
            }

            if (jv.TryGetValue<double>(out var d))
            {
                return cmp switch
                {
                    "eq" => Math.Abs(actual - (float)d) < 1e-5f,
                    "ne" => Math.Abs(actual - (float)d) >= 1e-5f,
                    "gt" => actual > d,
                    "ge" => actual >= d,
                    "lt" => actual < d,
                    "le" => actual <= d,
                    _ => false
                };
            }
        }

        return false;
    }
}
