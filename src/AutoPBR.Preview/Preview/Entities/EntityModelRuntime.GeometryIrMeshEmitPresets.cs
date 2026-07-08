using System.Numerics;

namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{
    /// <summary>
    /// Official JVM type names for bytecode-lifted geometry shards under
    /// <c>Data/minecraft-native/geometry/&lt;profile version label&gt;</c>.
    /// </summary>
    private static class GeometryIrModelJvmNames
    {
        internal const string Cod = "net.minecraft.client.model.animal.fish.CodModel";

        internal const string Salmon = "net.minecraft.client.model.animal.fish.SalmonModel";
    }

    /// <summary>
    /// Preset <see cref="GeometryIrMeshEmitOptions"/> for entities that use <see cref="GeometryIrDocumentLoader"/>
    /// plus <see cref="TryBuildMeshFromGeometryIr"/> body-layer emission (per-model atlas, part scales, pose overrides).
    /// </summary>
    private static class GeometryIrMeshEmitPresets
    {
        internal static GeometryIrMeshEmitOptions ForCod(BabyProfile p, float tailSway) =>
            new()
            {
                RootTransform = Matrix4x4.Identity,
                DefaultPartScale = p.BodyScale,
                AtlasWidth = 32,
                AtlasHeight = 32,
                OfficialJvmName = GeometryIrModelJvmNames.Cod,
                PreferCodegenCuboids = true,
                Fidelity = GeometryIrEmitFidelity.Viewport,
                PreviewDegenerateAxisThickness = 0.08f,
                ResolvePartScale = partId => partId switch
                {
                    "body" or "top_fin" => p.BodyScale,
                    "head" or "nose" => p.HeadScale,
                    "right_fin" or "left_fin" or "tail_fin" => p.LegScale,
                    _ => p.BodyScale,
                },
                TryGetPartPoseOverride = (partId, rest) => partId == "tail_fin"
                    ? EntityParityTemplate.Mul(rest, EntityParityTemplate.Ry(-tailSway * 0.35f))
                    : rest,
            };

        /// <summary>Same part scales and tail sway as <see cref="ForCod"/>; no preview thicken (IR box extents only).</summary>
        internal static GeometryIrMeshEmitOptions ForCodIrFidelity(BabyProfile p, float tailSway) =>
            ForCod(p, tailSway) with
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                PreviewDegenerateAxisThickness = 0f,
                PreferCodegenCuboids = false,
            };

        internal static GeometryIrMeshEmitOptions ForSalmon(BabyProfile p, float tailSway) =>
            new()
            {
                RootTransform = Matrix4x4.Identity,
                DefaultPartScale = p.BodyScale,
                AtlasWidth = 32,
                AtlasHeight = 32,
                OfficialJvmName = GeometryIrModelJvmNames.Salmon,
                PreferCodegenCuboids = true,
                Fidelity = GeometryIrEmitFidelity.Viewport,
                PreviewDegenerateAxisThickness = 0.08f,
                ResolvePartScale = partId => partId switch
                {
                    "body_front" or "body_back" or "top_front_fin" or "top_back_fin" => p.BodyScale,
                    "head" => p.HeadScale,
                    "back_fin" or "right_fin" or "left_fin" => p.LegScale,
                    _ => p.BodyScale,
                },
                TryGetPartPoseOverride = (partId, rest) => partId == "back_fin"
                    ? EntityParityTemplate.Mul(rest, EntityParityTemplate.Ry(-tailSway * 0.45f))
                    : rest,
            };

        internal static GeometryIrMeshEmitOptions ForSalmonIrFidelity(BabyProfile p, float tailSway) =>
            ForSalmon(p, tailSway) with
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                PreviewDegenerateAxisThickness = 0f,
                PreferCodegenCuboids = false,
            };
    }
}
