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

        if (IsKnownParityUnsafeDocument(geometryRoot))
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

    private static bool IsKnownParityUnsafeDocument(JsonElement geometryRoot)
    {
        var jvm = geometryRoot.TryGetProperty("officialJvmName", out var jvmEl)
            ? jvmEl.GetString()
            : null;
        var version = geometryRoot.TryGetProperty("versionLabel", out var versionEl)
            ? versionEl.GetString()
            : null;

        return string.Equals(version, MinecraftPreviewVersionGate.LegacyNativeProfileLabel, StringComparison.Ordinal) &&
               string.Equals(jvm, "net.minecraft.client.model.animal.dolphin.DolphinModel", StringComparison.Ordinal) &&
               LooksLikeLegacyDolphinPoseLiftCorruption(geometryRoot);
    }

    private static bool LooksLikeLegacyDolphinPoseLiftCorruption(JsonElement geometryRoot)
    {
        if (!TryFindPart(geometryRoot, "back_fin", out var backFin) ||
            !backFin.TryGetProperty("pose", out var pose) ||
            !pose.TryGetProperty("translation", out var translation) ||
            !pose.TryGetProperty("rotationEulerRad", out var rotation) ||
            translation.ValueKind != JsonValueKind.Array ||
            rotation.ValueKind != JsonValueKind.Array ||
            translation.GetArrayLength() < 3 ||
            rotation.GetArrayLength() < 3)
        {
            return false;
        }

        var tx = translation[0].GetDouble();
        var rx = rotation[0].GetDouble();
        return Math.Abs(tx - Math.PI / 3.0) < 1e-4 &&
               Math.Abs(rx) < 1e-6;
    }

    private static bool TryFindPart(JsonElement root, string partId, out JsonElement part)
    {
        part = default;
        if (!root.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var rootPart in roots.EnumerateArray())
        {
            if (TryFindPartRecursive(rootPart, partId, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPartRecursive(JsonElement candidate, string partId, out JsonElement part)
    {
        part = default;
        if (candidate.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
        {
            part = candidate;
            return true;
        }

        if (!candidate.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (TryFindPartRecursive(child, partId, out part))
            {
                return true;
            }
        }

        return false;
    }
}
