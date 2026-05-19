using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Shared DFS over geometry IR part trees — used by mesh emit, setupAnim cuboid order, and part-world indexing.
/// </summary>
internal static class GeometryIrMeshWalk
{
    public readonly struct CuboidVisitContext
    {
        public required string PartId { get; init; }
        public required Matrix4x4 PartWorld { get; init; }
        public required float PartScale { get; init; }
        public required JsonElement Cuboid { get; init; }
    }

    public static bool WalkRoots(
        JsonElement geometryRoot,
        Matrix4x4 rootTransform,
        GeometryIrMeshEmitOptions options,
        Func<CuboidVisitContext, bool>? onCuboid,
        Action<string, Matrix4x4>? onPartWorld,
        out string? failureReason)
    {
        failureReason = null;
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            failureReason = "missing roots array";
            return false;
        }

        foreach (var rootPart in roots.EnumerateArray())
        {
            if (!VisitPart(rootPart, rootTransform, options, onCuboid, onPartWorld, ref failureReason))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Composed part-origin translations (M41–M43) keyed by part <c>id</c>.</summary>
    public static bool TryCollectPartWorldTranslations(
        JsonElement geometryRoot,
        Matrix4x4 rootTransform,
        out Dictionary<string, Vector3> translationsByPartId,
        out string? failureReason) =>
        TryCollectBakedWorldTranslations(geometryRoot, out translationsByPartId, out failureReason) ||
        TryCollectPartWorldTranslationsByWalk(geometryRoot, rootTransform, out translationsByPartId, out failureReason);

    /// <summary>
    /// Uses Java reference bake <c>worldPose.translation</c> when every visited part exposes it (Phase 3A).
    /// </summary>
    public static bool TryCollectBakedWorldTranslations(
        JsonElement geometryRoot,
        out Dictionary<string, Vector3> translationsByPartId,
        out string? failureReason)
    {
        translationsByPartId = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        failureReason = null;
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!WalkBakedWorldTranslations(root, translationsByPartId, ref failureReason))
            {
                translationsByPartId = new Dictionary<string, Vector3>(StringComparer.Ordinal);
                return false;
            }
        }

        return translationsByPartId.Count > 0;
    }

    private static bool WalkBakedWorldTranslations(
        JsonElement part,
        Dictionary<string, Vector3> collected,
        ref string? failureReason)
    {
        if (part.TryGetProperty("id", out var idEl))
        {
            var id = idEl.GetString() ?? "";
            if (id.Length > 0)
            {
                if (!part.TryGetProperty("worldPose", out var worldPose) ||
                    worldPose.ValueKind != JsonValueKind.Object ||
                    !worldPose.TryGetProperty("translation", out var t) ||
                    t.ValueKind != JsonValueKind.Array ||
                    t.GetArrayLength() < 3)
                {
                    failureReason = $"part '{id}' missing worldPose.translation";
                    return false;
                }

                collected[id] = new Vector3(
                    (float)t[0].GetDouble(),
                    (float)t[1].GetDouble(),
                    (float)t[2].GetDouble());
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (!WalkBakedWorldTranslations(child, collected, ref failureReason))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryCollectPartWorldTranslationsByWalk(
        JsonElement geometryRoot,
        Matrix4x4 rootTransform,
        out Dictionary<string, Vector3> translationsByPartId,
        out string? failureReason)
    {
        var collected = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        var options = GeometryIrMeshEmitOptions.ForParity() with { RootTransform = rootTransform };
        var ok = WalkRoots(
            geometryRoot,
            rootTransform,
            options,
            onCuboid: null,
            onPartWorld: (partId, world) =>
            {
                collected[partId] = new Vector3(world.M41, world.M42, world.M43);
            },
            out failureReason);
        translationsByPartId = collected;
        return ok;
    }

    public static List<string> CollectCuboidOwnerPartIds(
        JsonElement geometryRoot,
        GeometryIrMeshEmitOptions options)
    {
        var list = new List<string>(32);
        WalkRoots(
            geometryRoot,
            options.RootTransform,
            options,
            ctx =>
            {
                list.Add(ctx.PartId);
                return true;
            },
            onPartWorld: null,
            out _);
        return list;
    }

    private static bool VisitPart(
        JsonElement part,
        Matrix4x4 parentWorld,
        GeometryIrMeshEmitOptions options,
        Func<CuboidVisitContext, bool>? onCuboid,
        Action<string, Matrix4x4>? onPartWorld,
        ref string? failureReason)
    {
        var world = parentWorld;
        if (part.TryGetProperty("pose", out var poseEl))
        {
            if (!CleanRoomEntityModelRuntime.TryComposePartPosePublic(poseEl, out var local))
            {
                failureReason = "compose part pose failed";
                return false;
            }

            world = Matrix4x4.Multiply(parentWorld, local);
        }

        var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        var skipPartCuboids = options.IncludePartIds is { Count: > 0 } include &&
                              !include.Contains(partId);

        if (options.TryGetPartPoseOverride is { } poseOverride && !skipPartCuboids)
        {
            world = poseOverride(partId, world);
        }

        if (partId.Length > 0)
        {
            onPartWorld?.Invoke(partId, world);
        }

        var partScale = options.ResolvePartScale?.Invoke(partId) ?? options.DefaultPartScale;
        if (!skipPartCuboids &&
            part.TryGetProperty("pose", out var poseForScale) &&
            poseForScale.TryGetProperty("uniformScale", out var scaleEl) &&
            scaleEl.ValueKind == JsonValueKind.Number)
        {
            partScale *= (float)scaleEl.GetDouble();
        }

        if (!skipPartCuboids &&
            part.TryGetProperty("cuboids", out var cuboids) &&
            cuboids.ValueKind == JsonValueKind.Array &&
            onCuboid is not null)
        {
            foreach (var cuboidEl in cuboids.EnumerateArray())
            {
                if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboidEl, out var emptyMask) && emptyMask.Length == 0)
                {
                    continue;
                }

                if (!onCuboid(new CuboidVisitContext
                    {
                        PartId = partId,
                        PartWorld = world,
                        PartScale = partScale,
                        Cuboid = cuboidEl
                    }))
                {
                    return false;
                }
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (!VisitPart(child, world, options, onCuboid, onPartWorld, ref failureReason))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
