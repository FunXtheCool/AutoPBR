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

        public static IReadOnlyDictionary<string, Matrix4x4> Build(JsonElement geometryRoot)

        {

            var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

            if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)

            {

                return map;

            }



            foreach (var rootPart in roots.EnumerateArray())

            {

                VisitPart(rootPart, Matrix4x4.Identity, map);

            }



            return map;

        }



        private static void VisitPart(JsonElement part, Matrix4x4 parentWorld, Dictionary<string, Matrix4x4> sink)

        {

            var world = parentWorld;

            if (part.TryGetProperty("pose", out var poseEl) &&
                TryComposePartPosePublic(poseEl, parentWorld, out var worldTexel))
            {
                world = worldTexel;
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

                VisitPart(child, world, sink);

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

        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var partId = partIds[i];
            if (!TryGetSetupAnimPartPose(partId, pose, out var partPose))
            {
                continue;
            }

            if (!TryBuildSetupAnimPartWorldDelta(partId, partPose, partOriginWorld, baselineParts, out var deltaWorld))
            {
                continue;
            }

            var e = merged.Elements[i];
            merged.Elements[i] = new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = EntityParityTemplate.Mul(deltaWorld, e.LocalToParent),
            };
        }

        return true;
    }
}
