using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>Decides whether lifted geometry IR is safe for parity-catalog mesh emit.</summary>
public enum GeometryIrLiftPolicyDecision
{
    Emit,
    EmitWithCaveat,
    RejectForParity
}

public static class GeometryIrLiftPolicy
{
    private static readonly HashSet<string> ParityAllowedApproxLiftKinds = new(StringComparer.Ordinal)
    {
        GeometryIrLiftKinds.TexCropStatic
    };

    /// <summary>
    /// Parity catalog requires all cuboids to be <see cref="GeometryIrLiftKinds.Exact"/> unless allowlisted.
    /// </summary>
    public static GeometryIrLiftPolicyDecision EvaluateDocument(JsonElement geometryRoot)
    {
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return GeometryIrLiftPolicyDecision.RejectForParity;
        }

        var hasCaveat = false;
        foreach (var part in roots.EnumerateArray())
        {
            if (!EvaluatePartTree(part, ref hasCaveat))
            {
                return GeometryIrLiftPolicyDecision.RejectForParity;
            }
        }

        return hasCaveat
            ? GeometryIrLiftPolicyDecision.EmitWithCaveat
            : GeometryIrLiftPolicyDecision.Emit;
    }

    private static bool EvaluatePartTree(JsonElement part, ref bool hasCaveat)
    {
        if (part.TryGetProperty("cuboids", out var cuboids) && cuboids.ValueKind == JsonValueKind.Array)
        {
            foreach (var cuboid in cuboids.EnumerateArray())
            {
                var kind = GeometryIrCuboidMetadata.GetLiftKind(cuboid);
                if (string.Equals(kind, GeometryIrLiftKinds.Exact, StringComparison.Ordinal))
                {
                    if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var mask) && mask.Length == 0)
                    {
                        return false;
                    }

                    continue;
                }

                if (ParityAllowedApproxLiftKinds.Contains(kind))
                {
                    hasCaveat = true;
                    continue;
                }

                return false;
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (!EvaluatePartTree(child, ref hasCaveat))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
