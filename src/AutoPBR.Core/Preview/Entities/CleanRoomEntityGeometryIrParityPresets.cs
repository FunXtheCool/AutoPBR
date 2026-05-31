using System.Numerics;

// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static class GeometryIrParityEmitPresetRegistry
    {
        private static readonly HashSet<string> HumanoidLeggingsParts = new(StringComparer.OrdinalIgnoreCase)
        {
            "body",
            "left_leg",
            "right_leg",
        };

        public static GeometryIrMeshEmitOptions CreateEmitOptions(
            string builderMethod,
            MinecraftNativeProfile profile,
            bool isBaby,
            string? resolvedOfficialJvm,
            int atlasW,
            int atlasH,
            float idlePhase01,
            float wave)
        {
            _ = idlePhase01;
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby, resolvedOfficialJvm);
            var geometryScale = string.Equals(builderMethod, "GuardianElder", StringComparison.OrdinalIgnoreCase)
                ? 2.35f
                : 1f;
            var opts = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with
            {
                DefaultPartScale = p.BodyScale * geometryScale,
                ResolvePartScale = partId => ResolveDefaultPartScale(partId, p) * geometryScale,
            };

            if (string.Equals(builderMethod, "EquipmentHumanoidLeggings", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { IncludePartIds = HumanoidLeggingsParts };
            }

            if ((string.Equals(builderMethod, "Horse", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(builderMethod, "DonkeyMuleHorse", StringComparison.OrdinalIgnoreCase)) &&
                !isBaby)
            {
                var neckBend = 0.25f + (wave * 0.2f);
                return opts with
                {
                    TryGetPartPoseOverride = (partId, world) => ApplyEquinePreviewPoseOverride(partId, world, neckBend, wave)
                };
            }

            return opts;
        }

        private static Matrix4x4 ApplyEquinePreviewPoseOverride(
            string partId,
            Matrix4x4 world,
            float neckBend,
            float wave) =>
            partId switch
            {
                "head_parts" => EntityParityTemplate.Mul(EntityParityTemplate.Rx(neckBend), world),
                "tail" => EntityParityTemplate.Mul(world, EntityParityTemplate.T(0f, 0f, wave * 0.5f)),
                _ => world
            };

        public static GeometryIrMeshEmitOptions CreateBreezeEmitOptions(
            MinecraftNativeProfile profile,
            string? resolvedOfficialJvm,
            int manifestAtlasW,
            int manifestAtlasH,
            bool isEyesTexturePath,
            bool isWindTexturePath,
            float idlePhase01,
            float wave,
            float animationTimeSeconds)
        {
            var swirl = idlePhase01 * 0.6f + wave * 0.2f;
            DefinitionAnimationPreviewSampling.TryResolveCatalogBreezeIdleWindTranslations(
                profile, animationTimeSeconds, out var windMid, out var windTop);
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby: false, resolvedOfficialJvm);
            var opts = GeometryIrMeshEmitOptions.ForParity(manifestAtlasW, manifestAtlasH) with
            {
                DefaultPartScale = p.BodyScale,
                ResolvePartScale = partId => ResolveDefaultPartScale(partId, p),
                ResolvePartAtlasDimensions = partId =>
                    IsBreezeWindPartId(partId) ? (128, 128) : (32, 32),
            };

            if (isEyesTexturePath)
            {
                return opts with
                {
                    ShouldEmitPartCuboids = static partId =>
                        string.Equals(partId, "eyes", StringComparison.OrdinalIgnoreCase),
                };
            }

            if (isWindTexturePath)
            {
                return opts with
                {
                    ShouldEmitPartCuboids = IsBreezeWindPartId,
                };
            }

            return opts with
            {
                ResolvePartTextureKey = partId =>
                    string.Equals(partId, "eyes", StringComparison.OrdinalIgnoreCase) ? "#eyes"
                    : IsBreezeWindPartId(partId) ? "#wind"
                    : null,
                TryGetPartPoseOverride = (partId, world) =>
                    ApplyBreezeCatalogEmitPoseOverride(partId, world, swirl, windMid, windTop),
            };
        }

        private static Matrix4x4 ApplyBreezeCatalogEmitPoseOverride(
            string partId,
            Matrix4x4 world,
            float swirl,
            Vector3 windMid,
            Vector3 windTop)
        {
            world = ApplyBreezeCatalogRodSwirl(partId, world, swirl);
            return partId switch
            {
                "wind_mid" => EntityParityTemplate.Mul(world, EntityParityTemplate.T(windMid.X, windMid.Y, windMid.Z)),
                "wind_top" => EntityParityTemplate.Mul(world, EntityParityTemplate.T(windTop.X, windTop.Y, windTop.Z)),
                _ => world,
            };
        }

        private static Matrix4x4 ApplyBreezeCatalogRodSwirl(string partId, Matrix4x4 world, float swirl) =>
            partId switch
            {
                "rod_1" => EntityParityTemplate.Mul(world, EntityParityTemplate.Rx(swirl)),
                "rod_2" => EntityParityTemplate.Mul(world, EntityParityTemplate.Rx(-swirl)),
                _ => world,
            };

        private static bool IsBreezeWindPartId(string partId) =>
            partId.StartsWith("wind", StringComparison.OrdinalIgnoreCase);
    }
}
