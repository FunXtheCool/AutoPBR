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
            float animationTimeSeconds = 0f,
            bool applyGeometryIrSetupAnimMotion = true)
        {
            _ = idlePhase01;
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby, resolvedOfficialJvm);
            var geometryScale = ResolveParityGeometryScale(builderMethod, normalizedAssetPath);
            var useUniformBabyRootScale = TryResolveUniformVanillaBabyRootScale(
                isBaby,
                resolvedOfficialJvm,
                p,
                geometryScale,
                out var uniformBabyRootScale);
            var cuboidScale = useUniformBabyRootScale ? 1f : p.BodyScale * geometryScale;
            var opts = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with
            {
                RootTransform = useUniformBabyRootScale
                    ? Matrix4x4.CreateScale(uniformBabyRootScale)
                    : Matrix4x4.Identity,
                DefaultPartScale = cuboidScale,
                ResolvePartScale = partId => useUniformBabyRootScale
                    ? 1f
                    : ResolveDefaultPartScale(partId, p) * geometryScale,
                PreviewApplyCubeDeformationInflate = applyGeometryIrSetupAnimMotion &&
                    (Math.Abs(animationTimeSeconds) > 1e-6f || Math.Abs(wave) > 1e-6f),
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

            // Vanilla axolotl legs/fins/gills use zero-thickness faceMask sheets. Gill Z depth is restored via
            // TryExpandAxolotlGillCuboidZExtents; preview thicken matches bee/chicken/creaking (thin solid, not ±1 gap).
            if (string.Equals(builderMethod, "Axolotl", StringComparison.OrdinalIgnoreCase))
            {
                // CubeDeformation(0.001) is static model geometry, not animation-driven; bind and anim
                // catalog emits must agree so GPU skinning matches CPU rebake at non-zero clocks.
                return opts with
                {
                    PreviewDegenerateAxisThickness = 0.06f,
                    PreviewApplyCubeDeformationInflate = true,
                };
            }

            if (string.Equals(builderMethod, "EnderDragon", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 1f };
            }

            if (string.Equals(builderMethod, "ConduitEntity", StringComparison.OrdinalIgnoreCase))
            {
                var centered = opts with { RootTransform = Matrix4x4.CreateTranslation(8f, 8f, 8f) };
                return resolvedOfficialJvm?.Contains("createEyeLayer", StringComparison.Ordinal) == true
                    ? centered with { PreviewDegenerateAxisThickness = 0.08f }
                    : centered;
            }

            if (string.Equals(builderMethod, "Camel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(builderMethod, "AdultCamel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(builderMethod, "BabyCamel", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 0.08f };
            }

            // Creaking head side panels and leg foot disks are texCrop zero-thickness sheets; thin preview
            // thicken only (same as bee legs) so north/south and up/down faces stay coplanar like Java.
            if (string.Equals(builderMethod, "Creaking", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 0.06f };
            }

            // Bee legs are north/south zero-thickness sheets; hand BuildBee uses ~0.12f Z depth for preview.
            if (string.Equals(builderMethod, "Bee", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 0.06f };
            }

            // Bat and allay wings are zero-thickness faceMask sheets in the runtime geometry IR.
            // Give them the same thin preview solid treatment as bee/chicken sheets so the two
            // opposing faces do not fight inside one draw batch.
            if (string.Equals(builderMethod, "Bat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(builderMethod, "Allay", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 0.06f };
            }

            // Chicken wings are north/south and up/down faceMask sheets; match bee/creaking preview thicken.
            if (string.Equals(builderMethod, "Chicken", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(builderMethod, "Flying", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { PreviewDegenerateAxisThickness = 0.06f };
            }

            if (string.Equals(builderMethod, "DecoratedPotEntity", StringComparison.OrdinalIgnoreCase))
            {
                return opts with
                {
                    PreviewDegenerateAxisThickness = DecoratedPotPreviewDegenerateAxisThickness,
                    ResolvePartAtlasDimensions = partId =>
                        IsDecoratedPotBasePartId(partId) ? (32, 32) : (16, 16),
                };
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

        public static GeometryIrMeshEmitOptions CreateCreakingEmitOptions(
            MinecraftNativeProfile profile,
            string? resolvedOfficialJvm,
            int manifestAtlasW,
            int manifestAtlasH,
            bool isEyesTexturePath,
            float idlePhase01,
            float wave,
            float animationTimeSeconds)
        {
            _ = idlePhase01;
            _ = wave;
            _ = animationTimeSeconds;
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby: false, resolvedOfficialJvm);
            var opts = GeometryIrMeshEmitOptions.ForParity(manifestAtlasW, manifestAtlasH) with
            {
                DefaultPartScale = p.BodyScale,
                ResolvePartScale = partId => ResolveDefaultPartScale(partId, p),
                PreviewDegenerateAxisThickness = 0.06f,
            };

            if (isEyesTexturePath)
            {
                return opts with
                {
                    ShouldEmitPartCuboids = static partId =>
                        string.Equals(partId, "head", StringComparison.OrdinalIgnoreCase),
                };
            }

            return opts;
        }

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

        /// <summary>
        /// Shared adult IR hosts (e.g. <c>NautilusModel</c>) keep lifted part offsets at adult texels while vanilla
        /// applies uniform <c>getAgeScale()</c> at render time. Fold that scale into <see cref="GeometryIrMeshEmitOptions.RootTransform"/>
        /// so pose offsets and cuboids shrink together; dedicated <c>Baby*Model</c> shards stay unit scale.
        /// </summary>
        private static bool TryResolveUniformVanillaBabyRootScale(
            bool isBaby,
            string? resolvedOfficialJvm,
            BabyProfile profile,
            float geometryScale,
            out float rootScale)
        {
            rootScale = 1f;
            if (!isBaby ||
                string.IsNullOrWhiteSpace(resolvedOfficialJvm) ||
                GeometryIrParityJvmResolver.SimpleClassNameContainsBaby(resolvedOfficialJvm))
            {
                return false;
            }

            if (profile.BodyScale != profile.HeadScale ||
                profile.BodyScale != profile.LegScale ||
                profile.BodyScale == 1f)
            {
                return false;
            }

            rootScale = profile.BodyScale * geometryScale;
            return rootScale != 1f;
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
            .WithOfficialJvmPoseComposeDefaults(officialJvm)
            with
            {
                OfficialJvmName = officialJvm,
                NormalizedAssetPath = normalizedAssetPath,
            };
    }
}
