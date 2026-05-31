using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

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

        var parentWorldBlock = EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose
            ? rootTransform
            : CleanRoomEntityModelRuntime.TexelRowAffineToBlock(rootTransform);

        foreach (var rootPart in roots.EnumerateArray())
        {
            if (!VisitPart(rootPart, parentWorldBlock, options, onCuboid, onPartWorld, ref failureReason))
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
                if (part.TryGetProperty("worldPose", out var worldPose) &&
                    worldPose.ValueKind == JsonValueKind.Object &&
                    worldPose.TryGetProperty("translation", out var worldT) &&
                    worldT.ValueKind == JsonValueKind.Array &&
                    worldT.GetArrayLength() >= 3)
                {
                    collected[id] = new Vector3(
                        (float)worldT[0].GetDouble(),
                        (float)worldT[1].GetDouble(),
                        (float)worldT[2].GetDouble());
                }
                else if (part.TryGetProperty("pose", out var pose) &&
                         pose.TryGetProperty("translation", out var poseT) &&
                         poseT.ValueKind == JsonValueKind.Array &&
                         poseT.GetArrayLength() >= 3)
                {
                    collected[id] = new Vector3(
                        (float)poseT[0].GetDouble(),
                        (float)poseT[1].GetDouble(),
                        (float)poseT[2].GetDouble());
                }
                else
                {
                    failureReason = $"part '{id}' missing worldPose.translation";
                    return false;
                }
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

    /// <summary>
    /// Composes part world from parent chain using Java reference bake semantics
    /// (<c>PartWorldPoseMath</c>: local = Er×T, world = parentWorld × local).
    /// </summary>
    private static bool TryComposePartWorldFromParent(
        JsonElement poseEl,
        Matrix4x4 parentWorldBlock,
        out Matrix4x4 worldBlock,
        out Matrix4x4 worldTexel,
        out string? failureReason)
    {
        failureReason = null;
        worldBlock = parentWorldBlock;
        worldTexel = EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose
            ? parentWorldBlock
            : CleanRoomEntityModelRuntime.BlockRowAffineToTexel(parentWorldBlock);

        if (!poseEl.ValueKind.Equals(JsonValueKind.Object))
        {
            return true;
        }

        if (EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose)
        {
            if (!CleanRoomEntityModelRuntime.TryComposePartPosePublic(poseEl, out var localTexel))
            {
                failureReason = "compose part pose failed";
                return false;
            }

            worldTexel = Matrix4x4.Multiply(parentWorldBlock, localTexel);
            worldBlock = worldTexel;
            return true;
        }

        if (!CleanRoomEntityModelRuntime.TryComposePartRenderLocalBlock(poseEl, out var localBlock, out failureReason))
        {
            return false;
        }

        worldBlock = Matrix4x4.Multiply(localBlock, parentWorldBlock);
        worldTexel = CleanRoomEntityModelRuntime.BlockRowAffineToTexel(worldBlock);
        return true;
    }

    private static bool VisitPart(
        JsonElement part,
        Matrix4x4 parentWorldBlock,
        GeometryIrMeshEmitOptions options,
        Func<CuboidVisitContext, bool>? onCuboid,
        Action<string, Matrix4x4>? onPartWorld,
        ref string? failureReason)
    {
        if (!TryComposePartWorldFromParent(
                part.TryGetProperty("pose", out var poseEl) ? poseEl : default,
                parentWorldBlock,
                out var worldBlock,
                out var worldTexel,
                out failureReason))
        {
            return false;
        }

        var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        var skipPartCuboids = options.ShouldEmitPartCuboids is { } shouldEmit
            ? !shouldEmit(partId)
            : options.IncludePartIds is { Count: > 0 } include && !include.Contains(partId);

        if (options.TryGetPartPoseOverride is { } poseOverride && !skipPartCuboids)
        {
            worldTexel = poseOverride(partId, worldTexel);
            worldBlock = EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose
                ? worldTexel
                : CleanRoomEntityModelRuntime.TexelRowAffineToBlock(worldTexel);
        }

        if (partId.Length > 0)
        {
            onPartWorld?.Invoke(partId, worldTexel);
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
                        PartWorld = worldTexel,
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
                if (!VisitPart(child, worldBlock, options, onCuboid, onPartWorld, ref failureReason))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
