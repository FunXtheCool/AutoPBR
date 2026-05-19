using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

internal static class GeometryIrLiftSummaryBuilder
{
    private const string LiftExact = "exact";
    public static JsonObject BuildFromRoots(JsonArray roots, int delegationDepth = 0)
    {
        var cuboidApprox = 0;
        var poseApprox = 0;
        Walk(roots, ref cuboidApprox, ref poseApprox);
        return new JsonObject
        {
            ["cuboidApproxCount"] = cuboidApprox,
            ["poseApproxCount"] = poseApprox,
            ["delegationDepth"] = delegationDepth
        };
    }

    private static bool HasNonBenignPoseWarnings(JsonArray warnings)
    {
        foreach (var w in warnings)
        {
            var code = w?.GetValue<string>();
            if (code is not null &&
                !string.Equals(code, "unknown_fload_zeroed", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Walk(JsonArray nodes, ref int cuboidApprox, ref int poseApprox)
    {
        foreach (var n in nodes)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            if (part["pose"] is JsonObject pose &&
                pose["liftWarnings"] is JsonArray pw &&
                pw.Count > 0 &&
                HasNonBenignPoseWarnings(pw))
            {
                poseApprox++;
            }

            if (part["cuboids"] is JsonArray cuboids)
            {
                foreach (var c in cuboids)
                {
                    if (c is not JsonObject co)
                    {
                        continue;
                    }

                    var kind = co["liftKind"]?.GetValue<string>() ?? LiftExact;
                    if (!string.Equals(kind, LiftExact, StringComparison.Ordinal))
                    {
                        cuboidApprox++;
                    }
                }
            }

            if (part["children"] is JsonArray kids)
            {
                Walk(kids, ref cuboidApprox, ref poseApprox);
            }
        }
    }
}
