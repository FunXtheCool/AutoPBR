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
            float wave,
            string? normalizedAssetPath = null,
            float animationTimeSeconds = 0f)
        {
            _ = idlePhase01;
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby, resolvedOfficialJvm);
            var geometryScale = ResolveParityGeometryScale(builderMethod, normalizedAssetPath);
            var opts = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with
            {
                DefaultPartScale = p.BodyScale * geometryScale,
                ResolvePartScale = partId => ResolveDefaultPartScale(partId, p) * geometryScale,
                PreviewApplyCubeDeformationInflate = true,
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

            // Vanilla axolotl legs/gills use zero-thickness sheets (faceMask); hand BuildAxolotl used thin solids
            // for stable preview UVs. IR parity emit keeps exact bytecode boxes — thicken degenerate axes only.
            if (string.Equals(builderMethod, "Axolotl", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 1f };
            }

            if (EntityPreviewPoseCatalog.IsIllagerBuilderMethod(builderMethod))
            {
                return IllagerPreviewPoseSupport.CreateIllagerParityEmitOptions(
                    opts,
                    normalizedAssetPath,
                    builderMethod,
                    idlePhase01,
                    animationTimeSeconds,
                    wave);
            }

            if (EntityPreviewPoseCatalog.IsHumanoidPoseBuilderMethod(builderMethod))
            {
                return HumanoidPreviewPoseSupport.CreateHumanoidParityEmitOptions(
                    opts,
                    builderMethod,
                    idlePhase01,
                    animationTimeSeconds,
                    wave);
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
                PreviewApplyCubeDeformationInflate = true,
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

        private static float ResolveParityGeometryScale(string builderMethod, string? normalizedAssetPath)
        {
            if (string.Equals(builderMethod, "GuardianElder", StringComparison.OrdinalIgnoreCase))
            {
                return 2.35f;
            }

            var path = normalizedAssetPath?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(path) &&
                path.Contains("/textures/entity/skeleton/wither_skeleton", StringComparison.OrdinalIgnoreCase))
            {
                return 1.2f;
            }

            return 1f;
        }
    }

    /// <summary>
    /// Pose-aware parity emit options for cuboid-owner part id walks (rebake placement, grounding).
    /// </summary>
    internal static GeometryIrMeshEmitOptions CreateParityCatalogPartIdResolveEmitOptions(
        string builderMethod,
        MinecraftNativeProfile profile,
        bool isBaby,
        string officialJvm,
        int atlasW,
        int atlasH,
        string normalizedAssetPath,
        string? previewPoseId)
    {
        using var poseScope = string.IsNullOrWhiteSpace(previewPoseId)
            ? null
            : EntityPreviewBuildContext.UsePose(previewPoseId);

        return GeometryIrParityEmitPresetRegistry.CreateEmitOptions(
                builderMethod,
                profile,
                isBaby,
                officialJvm,
                atlasW,
                atlasH,
                idlePhase01: 0f,
                wave: 0f,
                normalizedAssetPath: normalizedAssetPath,
                animationTimeSeconds: 0f)
            .WithOfficialJvmPoseComposeDefaults(officialJvm);
    }
}
