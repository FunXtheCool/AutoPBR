using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>

    /// Part-origin world matrices for geometry IR (same DFS visit order as geometry IR mesh emission).

    /// </summary>

    private static class GeometryIrPartWorldPoseIndex

    {

        public static Dictionary<string, Matrix4x4> Build(JsonElement geometryRoot, in GeometryIrMeshEmitOptions? emitOptions = null)

        {

            var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

            if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)

            {

                return map;

            }

            var useColumn = emitOptions?.ResolveUseColumnTranslationTimesRotationPartPose() == true;

            foreach (var rootPart in roots.EnumerateArray())

            {

                VisitPart(rootPart, Matrix4x4.Identity, map, useColumn);

            }



            return map;

        }

        /// <summary>Maps each part <c>id</c> to its parent part <c>id</c> (model root children use <c>null</c> parent).</summary>
        public static Dictionary<string, string?> BuildParentMap(JsonElement geometryRoot)
        {
            var map = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
            {
                return map;
            }

            foreach (var rootPart in roots.EnumerateArray())
            {
                VisitParents(rootPart, parentId: null, map);
            }

            return map;
        }

        private static void VisitParents(JsonElement part, string? parentId, Dictionary<string, string?> sink)
        {
            var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (partId.Length > 0)
            {
                sink[partId] = parentId;
            }

            if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var child in children.EnumerateArray())
            {
                VisitParents(child, partId.Length > 0 ? partId : parentId, sink);
            }
        }



        private static void VisitPart(JsonElement part, Matrix4x4 parentWorld, Dictionary<string, Matrix4x4> sink, bool useColumnPose)

        {

            var world = parentWorld;

            if (part.TryGetProperty("pose", out var poseEl))
            {
                if (useColumnPose)
                {
                    if (TryComposeColumnPartPose(poseEl, parentWorld, out world, out _))
                    {
                    }
                }
                else if (TryComposePartPosePublic(poseEl, parentWorld, out var worldTexel))
                {
                    world = worldTexel;
                }
            }



            var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";

            if (partId.Length > 0)

            {

                sink[partId] = world;

            }



            if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)

            {

                return;

            }



            foreach (var child in children.EnumerateArray())

            {

                VisitPart(child, world, sink, useColumnPose);

            }

        }

    }



    /// <summary>

    /// Applies lifted setupAnim part poses to IR-emitted cuboids (part.id keys, pivot from part-origin world index).

    /// </summary>

    private static bool ApplySetupAnimToGeometryIrMesh(

        MergedJavaBlockModel merged,

        JsonElement geometryRoot,

        VanillaSetupAnimRuntime.PoseResult pose,

        IReadOnlyDictionary<string, Matrix4x4> partOriginWorld,

        GeometryIrMeshEmitOptions emitOptions)

    {

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);

        if (partIds.Count != merged.Elements.Count)

        {

            return false;

        }

        var baselineParts = BuildSetupAnimBaselineParts(geometryRoot);
        var partDeltas = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var partId in partIds.Distinct(StringComparer.Ordinal))
        {
            if (!TryGetSetupAnimPartPose(partId, pose, out var partPose) ||
                !TryBuildSetupAnimPartWorldDelta(partId, partPose, partOriginWorld, baselineParts, out var deltaWorld))
            {
                continue;
            }

            partDeltas[partId] = deltaWorld;
        }

        if (partDeltas.Count == 0)
        {
            return false;
        }

        var parentMap = GeometryIrPartWorldPoseIndex.BuildParentMap(geometryRoot);
        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var partId = partIds[i];
            if (!TryComposeSetupAnimAncestorDeltas(partId, parentMap, partDeltas, out var composedDelta))
            {
                continue;
            }

            var e = merged.Elements[i];
            merged.Elements[i] = new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = EntityParityTemplate.Mul(composedDelta, e.LocalToParent),
            };
        }

        return true;
    }

    /// <summary>
    /// Root-to-part chain: multiply each animated ancestor delta so child cuboids inherit parent setupAnim motion
    /// (e.g. dolphin pectoral fins under a pitching <c>body</c>).
    /// </summary>
    private static bool TryComposeSetupAnimAncestorDeltas(
        string partId,
        IReadOnlyDictionary<string, string?> parentMap,
        IReadOnlyDictionary<string, Matrix4x4> partDeltas,
        out Matrix4x4 composedDelta)
    {
        composedDelta = Matrix4x4.Identity;
        var chain = new List<string>();
        var current = partId;
        while (parentMap.TryGetValue(current, out var parent) && parent is not null)
        {
            chain.Add(parent);
            current = parent;
        }

        chain.Reverse();
        var any = false;
        foreach (var ancestorId in chain)
        {
            if (!partDeltas.TryGetValue(ancestorId, out var delta))
            {
                continue;
            }

            composedDelta = EntityParityTemplate.Mul(delta, composedDelta);
            any = true;
        }

        if (partDeltas.TryGetValue(partId, out var selfDelta))
        {
            composedDelta = EntityParityTemplate.Mul(selfDelta, composedDelta);
            any = true;
        }

        return any;
    }
}
