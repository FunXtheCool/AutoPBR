using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.


using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Entity texture dispatch: <see cref="TryDispatchEntityStaticMeshBuild"/>, <see cref="TryBuildSpecific"/> branch ordering
/// (stable GPU bone slots — do not reorder branches without updating routing inventory tests).
/// </summary>
internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryDispatchEntityStaticMeshBuild(
        string norm,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        EntityGpuBoneDispatchRoute? routeCache,
        bool applyGeometryIrSetupAnimMotion,
        out EntityGpuBoneDispatchRoute discoveredRoute,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance)
    {
        mergedModel = null!;
        discoveredRoute = default;
        meshProvenance = default;

        if (EntityTextureParityCatalog.IsCatalogued(norm) &&
            routeCache is { Kind: EntityGpuBoneDispatchKind.ParityCatalog, ParityBuilderMethod: var cachedParityMethod } &&
            !string.IsNullOrEmpty(cachedParityMethod))
        {
            var cachedRule = EntityTextureParityCatalog.ResolveRule(norm, stem);
            if (cachedRule is not null &&
                TryBuildParityCatalogMeshFromGeometryIr(
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    cachedRule,
                    applyGeometryIrSetupAnimMotion,
                    out mergedModel,
                    out var irJvm))
            {
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(cachedParityMethod);
                meshProvenance = new(PreviewMeshDriverKind.RuntimeGeometryIrJson, irJvm);
                return true;
            }

            if (cachedRule is not null &&
                ShouldSuppressHandBuiltParityFallback(profile, cachedRule, norm, stem, isBaby, out _))
            {
                mergedModel = null!;
                return false;
            }

            if (TryInvokeParityCatalogBuilder(
                    cachedParityMethod,
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    out mergedModel))
            {
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(cachedParityMethod);
                meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"catalog · {cachedParityMethod}");
                return true;
            }
        }

        if (EntityTextureParityCatalog.IsCatalogued(norm))
        {
            if (EntityTextureParityCatalog.ResolveRule(norm, stem) is not { } parityRule)
            {
                mergedModel = null!;
                return false;
            }

            if (TryBuildParityCatalogMeshFromGeometryIr(
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    parityRule,
                    applyGeometryIrSetupAnimMotion,
                    out mergedModel,
                    out var irJvm))
            {
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(parityRule.BuilderMethod);
                meshProvenance = new(PreviewMeshDriverKind.RuntimeGeometryIrJson, irJvm);
                return true;
            }

            if (ShouldSuppressHandBuiltParityFallback(
                    profile, parityRule, norm, stem, isBaby, out _))
            {
                mergedModel = null!;
                return false;
            }

            if (TryInvokeParityCatalogBuilder(
                    parityRule.BuilderMethod,
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    out mergedModel))
            {
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(parityRule.BuilderMethod);
                meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"catalog · {parityRule.BuilderMethod}");
                return true;
            }

            return false;
        }

        var resolvedFamily = ResolveFamily(stem);
        if (routeCache is { Kind: EntityGpuBoneDispatchKind.FamilyFallback, Family: var cachedFamily } &&
            MapGpuFamily(cachedFamily) == resolvedFamily &&
            TryBuildForFamily(
                resolvedFamily,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                out mergedModel))
        {
            discoveredRoute = EntityGpuBoneDispatchRoute.ForFamily(cachedFamily);
            meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"family · {cachedFamily}");
            return true;
        }

        if (routeCache is
            {
                Kind: EntityGpuBoneDispatchKind.SpecificModelSlot,
                SpecificSlot: > 0 and var cachedSpecificSlot
            } &&
            TryBuildSpecific(
                norm,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                cachedSpecificSlot,
                out mergedModel,
                out _))
        {
            discoveredRoute = EntityGpuBoneDispatchRoute.ForSpecificSlot(cachedSpecificSlot);
            meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"specific #{cachedSpecificSlot}");
            return true;
        }

        if (TryBuildSpecific(
                norm,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                null,
                out mergedModel,
                out var specificSlotHit))
        {
            if (specificSlotHit > 0)
            {
                discoveredRoute = EntityGpuBoneDispatchRoute.ForSpecificSlot(specificSlotHit);
                meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"specific #{specificSlotHit}");
            }
            else
            {
                meshProvenance = new(PreviewMeshDriverKind.CleanRoom, "specific");
            }

            return true;
        }

        if (TryBuildForFamily(resolvedFamily, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, out mergedModel))
        {
            discoveredRoute = EntityGpuBoneDispatchRoute.ForFamily(ToGpuFamily(resolvedFamily));
            meshProvenance = new(PreviewMeshDriverKind.CleanRoom, $"family · {resolvedFamily}");
            return true;
        }

        mergedModel = null!;
        return false;
    }

    private static EntityRigFamily MapGpuFamily(EntityGpuBoneFamily family) =>
        family switch
        {
            EntityGpuBoneFamily.Humanoid => EntityRigFamily.Humanoid,
            EntityGpuBoneFamily.Quadruped => EntityRigFamily.Quadruped,
            EntityGpuBoneFamily.Flying => EntityRigFamily.Flying,
            EntityGpuBoneFamily.Aquatic => EntityRigFamily.Aquatic,
            _ => EntityRigFamily.Unknown
        };

    private static EntityGpuBoneFamily ToGpuFamily(EntityRigFamily family) =>
        family switch
        {
            EntityRigFamily.Humanoid => EntityGpuBoneFamily.Humanoid,
            EntityRigFamily.Quadruped => EntityGpuBoneFamily.Quadruped,
            EntityRigFamily.Flying => EntityGpuBoneFamily.Flying,
            EntityRigFamily.Aquatic => EntityGpuBoneFamily.Aquatic,
            _ => default
        };

    internal static EntityPreviewRouteKind ClassifyEntityTextureRoute(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds)
    {
        var norm = entityTextureAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return EntityPreviewRouteKind.InvalidPath;
        }

        var texRef = ToTextureRef(norm);
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = LooksLikeBabyTexture(stem, norm);
        if (EntityTextureParityCatalog.IsCatalogued(norm))
        {
            if (EntityTextureParityCatalog.ResolveRule(norm, stem) is not { } parityRule)
            {
                return EntityPreviewRouteKind.UnknownNoMesh;
            }

            if (TryBuildParityCatalogMeshFromGeometryIr(
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    parityRule,
                    applyGeometryIrSetupAnimMotion: false,
                    out _,
                    out _))
            {
                return EntityPreviewRouteKind.SpecificMesh;
            }

            if (ShouldSuppressHandBuiltParityFallback(profile, parityRule, norm, stem, isBaby, out _))
            {
                return EntityPreviewRouteKind.UnknownNoMesh;
            }

            return TryInvokeParityCatalogBuilder(
                parityRule.BuilderMethod,
                norm,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                out _)
                ? EntityPreviewRouteKind.SpecificMesh
                : EntityPreviewRouteKind.UnknownNoMesh;
        }

        if (TryBuildSpecific(
                norm,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                null,
                out _,
                out _))
        {
            return EntityPreviewRouteKind.SpecificMesh;
        }

        return ResolveFamily(stem) switch
        {
            EntityRigFamily.Humanoid => EntityPreviewRouteKind.HumanoidFamilyFallback,
            EntityRigFamily.Quadruped => EntityPreviewRouteKind.QuadrupedFamilyFallback,
            EntityRigFamily.Flying => EntityPreviewRouteKind.FlyingFamilyFallback,
            EntityRigFamily.Aquatic => EntityPreviewRouteKind.AquaticFamilyFallback,
            _ => EntityPreviewRouteKind.UnknownNoMesh,
        };
    }

    private static bool TryGpuBoneSpecificDispatchSlot(ref int slot, int? fastPathOnlySlot, out int dispatchSlot)
    {
        dispatchSlot = ++slot;
        return !fastPathOnlySlot.HasValue || fastPathOnlySlot.GetValueOrDefault() == dispatchSlot;
    }

    private static bool TryBuildSpecific(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        int? fastPathOnlySlot,
        out MergedJavaBlockModel merged,
        out int specificRouteSlotHit)
    {
        merged = null!;
        specificRouteSlotHit = 0;
        var wave = Wave(animationTimeSeconds, 0.8f);
        var slot = 0;

        // Before broad "zombie"/"skeleton" humanoid stems: mob skins that contain those substrings.
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out var gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/nautilus/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildNautilusMob(texRef, profile, isBaby, animationTimeSeconds);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("horse_zombie", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("horse_skeleton", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHorse(texRef, profile, isBaby, neckBend: 0f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("zombie", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildZombieHumanoid(texRef, profile, isBaby, armLift: 1.2f + idlePhase01 * 0.6f + wave * 0.2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("villager", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.18f + wave * 0.03f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("wandering_trader", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.2f + wave * 0.04f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("enderman", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnderman(texRef, profile, isBaby, armLift: 0.16f + wave * 0.05f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("witch", StringComparison.OrdinalIgnoreCase))
            {
                var (wWalkPos, wWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                var witchEntityId = stem.GetHashCode(StringComparison.Ordinal);
                merged = BuildWitch(
                    texRef,
                    profile,
                    isBaby,
                    yRotDegrees: wave * 10f,
                    xRotDegrees: idlePhase01 * 12f + wave * 6f,
                    walkAnimationPos: wWalkPos,
                    walkAnimationSpeed: wWalkSpeed,
                    entityId: witchEntityId,
                    ageInTicks: animationTimeSeconds * 20f,
                    isHoldingItem: true);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("evoker", StringComparison.OrdinalIgnoreCase) &&
                !stem.Contains("fang", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEvoker(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("vindicator", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVindicator(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("illusioner", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.Crossed);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("pillager", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("illager", StringComparison.OrdinalIgnoreCase))
            {
                var illagerPose = stem.Contains("pillager", StringComparison.OrdinalIgnoreCase)
                    ? IllagerPreviewArmPoseKind.CrossbowHold
                    : IllagerPreviewArmPoseKind.Crossed;
                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, illagerPose);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("cow", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                var headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                if (normalizedAssetPath.Contains("/textures/entity/cow/cow_cold", StringComparison.OrdinalIgnoreCase))
                {
                    merged = BuildColdCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }
                else if (normalizedAssetPath.Contains("/textures/entity/cow/cow_warm", StringComparison.OrdinalIgnoreCase))
                {
                    merged = BuildWarmCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }
                else
                {
                    merged = BuildCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }

                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("mooshroom", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildCow(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.30f + wave * 0.12f,
                hasHorns: true,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("wolf", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildWolf(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.45f + wave * 0.20f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("goat", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildGoat(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.30f + wave * 0.15f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("zoglin", StringComparison.OrdinalIgnoreCase))
            {
                float rh, lh, rf, lf, headPitch;
                if (isBaby)
                {
                    (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                    headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                }
                else
                {
                    rh = lh = rf = lf = 0f;
                    headPitch = 0f;
                }

                merged = BuildZoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("hoglin", StringComparison.OrdinalIgnoreCase))
            {
                float rh, lh, rf, lf, headPitch;
                if (isBaby)
                {
                    (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                    headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                }
                else
                {
                    rh = lh = rf = lf = 0f;
                    headPitch = 0f;
                }

                merged = BuildHoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("sniffer", StringComparison.OrdinalIgnoreCase))
            {
                var snifferHead = idlePhase01 * 0.12f + wave * 0.08f;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferLongSniffHeadRotationDegrees(profile, animationTimeSeconds, out var sniffHeadDeg))
                {
                    snifferHead += sniffHeadDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(profile, animationTimeSeconds, out var walkHeadDeg))
                {
                    snifferHead += walkHeadDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkBodyRotationDegrees(profile, animationTimeSeconds, out var walkBodyDeg))
                {
                    snifferHead += walkBodyDeg.X * (MathF.PI / 180f) * 0.15f;
                }

                var sniffWalkRf = 0f;
                var sniffWalkLf = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffRfDeg))
                {
                    sniffWalkRf = sniffRfDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffLfDeg))
                {
                    sniffWalkLf = sniffLfDeg.X * (MathF.PI / 180f);
                }

                const float sniffDegToRad = MathF.PI / 180f;
                var sniffWalkLm = Vector3.Zero;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftMidLegRotationDegrees(profile, animationTimeSeconds, out var sniffLmDeg))
                {
                    sniffWalkLm = new Vector3(
                        sniffLmDeg.X * sniffDegToRad,
                        sniffLmDeg.Y * sniffDegToRad,
                        sniffLmDeg.Z * sniffDegToRad);
                }

                merged = BuildSniffer(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: snifferHead,
                    walkRightFrontLegPitchRad: sniffWalkRf,
                    walkLeftFrontLegPitchRad: sniffWalkLf,
                    walkLeftMidLegEulerRad: sniffWalkLm);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("wither", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildWither(texRef, profile, isBaby, wave: idlePhase01 * 0.35f + wave * 0.12f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("warden", StringComparison.OrdinalIgnoreCase))
            {
                var wardenSway = idlePhase01 * 0.30f + wave * 0.10f;
                if (DefinitionAnimationPreviewSampling.TrySampleWardenSniffBodyRotationDegrees(profile, animationTimeSeconds, out var wardenBodyDeg))
                {
                    wardenSway += wardenBodyDeg.Z * (MathF.PI / 180f) * 0.15f;
                }

                merged = BuildWarden(texRef, profile, isBaby, sway: wardenSway);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("magma_cube", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("magmacube", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildMagmaCube(texRef, profile, isBaby, squish: MathF.Max(0f, wave));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("slime", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSlime(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("silverfish", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSilverfish(texRef, profile, isBaby, ageInTicks: wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("endermite", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndermite(texRef, profile, isBaby, ageInTicks: wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("shulker_bullet", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/shulker/spark", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShulkerBullet(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 45f, xRotDegrees: wave * 25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("shulker", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShulker(
                    texRef,
                    profile,
                    isBaby,
                    peekAmount: Math.Clamp((wave + 1f) * 0.5f, 0f, 1f),
                    ageInTicks: animationTimeSeconds * 20f,
                    xRotDegrees: 0f,
                    yHeadRotDegrees: 180f,
                    yBodyRotDegrees: 0f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("snow_golem", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("snowman", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSnowGolem(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 40f, xRotDegrees: 0f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("iron_golem", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("irongolem", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildIronGolem(
                    texRef,
                    profile,
                    isBaby,
                    attackTicksRemaining: 0f,
                    offerFlowerTick: 0,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    yRotDegrees: animationTimeSeconds * 28f,
                    xRotDegrees: 0f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("end_crystal", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_crystal/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndCrystal(texRef, profile, isBaby, spin: idlePhase01 * 180f + animationTimeSeconds * 30f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("evoker_fangs", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/illager/evoker_fangs", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEvokerFangs(texRef, profile, isBaby, bitePhase: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("spit", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLlamaSpit(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("arrow", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("wind_charge", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/projectiles/wind_charge", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildWindCharge(texRef, profile, isBaby, spin: animationTimeSeconds);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("trident", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/trident", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTrident(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("shield", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/shield", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/shield_base.png", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/shield_base_nopattern.png", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShield(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("banner", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/banner/", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/banner_base.png", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: normalizedAssetPath.Contains("/textures/entity/banner_base", StringComparison.OrdinalIgnoreCase));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("bed", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bed/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBed(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/equipment/", StringComparison.OrdinalIgnoreCase) &&
                !normalizedAssetPath.Contains("/textures/entity/equipment/happy_ghast_body/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("skull", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/skull/", StringComparison.OrdinalIgnoreCase))
            {
                merged = stem.Contains("piglin", StringComparison.OrdinalIgnoreCase)
                ? BuildPiglinSkull(texRef, profile, isBaby, headPitch: idlePhase01 * 0.2f + wave * 0.1f)
                : BuildSkull(texRef, profile, isBaby, headPitch: idlePhase01 * 0.2f + wave * 0.1f);

                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("bell", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bell/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBell(texRef, profile, isBaby, swing: idlePhase01 * 0.5f + wave * 0.15f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("minecart", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildMinecart(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/boat/", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBoat(
                texRef,
                profile,
                isBaby,
                isChestBoat: normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("leash_knot", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("lead_knot", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/leash_knot", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/lead_knot", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLeashKnot(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/armorstand/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildArmorStand(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("ravager", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildRavager(
                    texRef,
                    profile,
                    isBaby,
                    xRotDegrees: idlePhase01 * 10f + wave * 6f,
                    yRotDegrees: animationTimeSeconds * 32f,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    attackTicksRemaining: 0f,
                    stunnedTicksRemaining: 0f,
                    roarAnimation: Math.Clamp(idlePhase01 * 0.35f + wave * 0.25f, 0f, 1f));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("armadillo", StringComparison.OrdinalIgnoreCase))
            {
                var armadilloTailWalkRad = 0f;
                if (isBaby)
                {
                    if (DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloWalkTailRotationDegrees(profile, animationTimeSeconds, out var babyTailDeg))
                    {
                        armadilloTailWalkRad = babyTailDeg.X * (MathF.PI / 180f);
                    }
                }
                else if (DefinitionAnimationPreviewSampling.TrySampleArmadilloWalkTailRotationDegrees(profile, animationTimeSeconds, out var adultTailDeg))
                {
                    armadilloTailWalkRad = adultTailDeg.X * (MathF.PI / 180f);
                }

                merged = BuildArmadillo(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: idlePhase01 * 0.14f + wave * 0.08f,
                    tailWalkPitchRad: armadilloTailWalkRad);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/breeze/", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("breeze", StringComparison.OrdinalIgnoreCase))
            {
                var shootHeadPitchRad = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(profile, animationTimeSeconds, out var shootHeadDeg))
                {
                    shootHeadPitchRad = shootHeadDeg.X * (MathF.PI / 180f);
                }

                var shootHeadPos = Vector3.Zero;
                if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadPosition(profile, animationTimeSeconds, out var shootHeadTranslation))
                {
                    shootHeadPos = shootHeadTranslation;
                }

                merged = BuildBreeze(
                normalizedAssetPath,
                texRef,
                profile,
                isBaby,
                swirl: idlePhase01 * 0.6f + wave * 0.2f,
                windAnimTimeSeconds: animationTimeSeconds,
                shootHeadAdditivePitchRad: shootHeadPitchRad,
                shootHeadAdditiveTranslate: shootHeadPos);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("llama", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLlama(texRef, profile, isBaby, neckBend: idlePhase01 * 0.30f + wave * 0.10f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("camel", StringComparison.OrdinalIgnoreCase))
            {
                var babyCamelHeadZ = 0f;
                if (isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkHeadPosition(profile, animationTimeSeconds, out var camelBabyHeadPos))
                {
                    babyCamelHeadZ = camelBabyHeadPos.Z;
                }

                var camelRootRollRad = 0f;
                if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelWalkRootRotationDegrees(profile, animationTimeSeconds, out var camelRootDeg))
                {
                    camelRootRollRad = camelRootDeg.Z * (MathF.PI / 180f);
                }

                merged = BuildCamel(
                    texRef,
                    profile,
                    isBaby,
                    neckBend: idlePhase01 * 0.25f + wave * 0.12f,
                    animationTimeSeconds,
                    idlePhase01,
                    babyWalkHeadTranslateZ: babyCamelHeadZ,
                    adultWalkRootRollRad: camelRootRollRad);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("panda", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPanda(
                texRef,
                profile,
                isBaby,
                bodyRoll: idlePhase01 * 0.20f + wave * 0.10f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("polar_bear", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("polarbear", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPolarBear(
                texRef,
                profile,
                isBaby,
                headLift: idlePhase01 * 0.22f + wave * 0.10f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("zombified_piglin", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildZombifiedPiglin(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: idlePhase01 * 0.24f + wave * 0.10f,
                    armLift: idlePhase01 * 0.28f + wave * 0.10f,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    ageInTicks: animationTimeSeconds * 20f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        // Stem "piglin" contains substring "pig"; route before the pig mob check.
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("piglin", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPiglin(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: idlePhase01 * 0.28f + wave * 0.11f,
                    armLift: idlePhase01 * 0.35f + wave * 0.12f,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    ageInTicks: animationTimeSeconds * 20f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("pig_cold", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/pig/pig_cold", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildColdPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("pig", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("sheep", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildSheep(texRef, profile, isBaby, grazeDip: 0.35f + idlePhase01 * 0.25f + wave * 0.25f, rh, lh, rf, lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if ((stem.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)) &&
                (stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildHorseDonkeyMule(
                    texRef,
                    profile,
                    isBaby,
                    neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if ((stem.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)) &&
                !(stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildHorse(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("rabbit", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/rabbit/", StringComparison.OrdinalIgnoreCase))
            {
                var hop = Math.Clamp(0.25f + wave * 0.25f + ComputePreviewRabbitHopSinTerm(animationTimeSeconds), -0.75f, 0.75f);
                var tiltOk = isBaby
                    ? DefinitionAnimationPreviewSampling.TrySampleBabyRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out var tiltBody)
                    : DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out tiltBody);
                if (tiltOk)
                {
                    hop = Math.Clamp(hop + tiltBody.Y * 0.18f, -0.75f, 0.75f);
                }

                if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleRabbitHopFrontLegsPosition(profile, animationTimeSeconds, out var hopFrontLegs))
                {
                    hop = Math.Clamp(hop + hopFrontLegs.Y * 0.12f + hopFrontLegs.Z * 0.06f, -0.75f, 0.75f);
                }

                merged = BuildRabbit(texRef, profile, isBaby, hopCompress: hop);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("dolphin", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDolphin(
                texRef,
                profile,
                isBaby,
                swimSway: idlePhase01 * 0.6f + wave * 0.25f + ComputePreviewDolphinSwimOscillation(animationTimeSeconds));
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("axolotl", StringComparison.OrdinalIgnoreCase))
            {
                if (TryBuildAxolotlMeshPreferGeometryIr(
                        normalizedAssetPath,
                        stem,
                        texRef,
                        profile,
                        isBaby,
                        idlePhase01,
                        animationTimeSeconds,
                        out merged))
                {
                    specificRouteSlotHit = gpuBoneDispatchSlot;
                    return true;
                }

                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildAxolotl(
                texRef,
                profile,
                isBaby,
                idleBob: idlePhase01 * 0.12f + wave * 0.06f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        // Vanilla OcelotModel matches CatModel cuboids; stem is "ocelot" (substring "cat" is false).
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("cat", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("ocelot", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildCat(
                texRef,
                profile,
                isBaby,
                headTilt: idlePhase01 * 0.2f + wave * 0.1f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("fox", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildFox(
                texRef,
                profile,
                isBaby,
                tailLift: 0f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("chicken", StringComparison.OrdinalIgnoreCase))
            {
                ComputeChickenParityPreviewDrivers(
                    animationTimeSeconds,
                    idlePhase01,
                    wave,
                    out var headPitchRad,
                    out var headYawRad,
                    out var wingZ,
                    out var rLeg,
                    out var lLeg);
                merged = IsAdultColdChickenStem(stem) && !isBaby
                    ? BuildColdChicken(
                        texRef,
                        profile,
                        headPitchRad: headPitchRad,
                        headYawRad: headYawRad,
                        wingZRadians: wingZ,
                        rightLegPitchRad: rLeg,
                        leftLegPitchRad: lLeg)
                    : BuildChicken(
                        texRef,
                        profile,
                        isBaby,
                        headPitchRad: headPitchRad,
                        headYawRad: headYawRad,
                        wingZRadians: wingZ,
                        rightLegPitchRad: rLeg,
                        leftLegPitchRad: lLeg);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("creeper", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildCreeper(texRef, profile, isBaby, bodyBob: idlePhase01 * 0.2f + wave * 0.1f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("spider", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSpider(texRef, profile, isBaby, legSpread: 0.45f + idlePhase01 * 0.25f + wave * 0.2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("dragon_fireball", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/enderdragon/dragon_fireball", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDragonFireball(texRef, profile, isBaby, framePick01: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("enderdragon", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("dragon", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnderDragon(texRef, profile, isBaby, wingSweep: 0.4f + idlePhase01 * 0.35f + wave * 0.15f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("bat", StringComparison.OrdinalIgnoreCase))
            {
                var batWingFold = idlePhase01 * 0.35f + wave * 0.15f;
                var batRightRy = batWingFold * 0.5f;
                var batLeftRy = batWingFold * 0.5f;
                if (DefinitionAnimationPreviewSampling.TrySampleBatFlyingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRWingDeg) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatFlyingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batLWingDeg))
                {
                    batRightRy = batRWingDeg.Y * (MathF.PI / 180f);
                    batLeftRy = batLWingDeg.Y * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRestRWingDeg) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batRestLWingDeg))
                {
                    const float batRestingWingBlend = 0.22f;
                    var restRight = batRestRWingDeg.Y * (MathF.PI / 180f);
                    var restLeft = batRestLWingDeg.Y * (MathF.PI / 180f);
                    batRightRy = batRightRy + (restRight - batRightRy) * batRestingWingBlend;
                    batLeftRy = batLeftRy + (restLeft - batLeftRy) * batRestingWingBlend;
                }

                var batRightWingZ = 0f;
                var batLeftWingZ = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingPosition(profile, animationTimeSeconds, out var batRestRPos) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingPosition(profile, animationTimeSeconds, out var batRestLPos))
                {
                    const float batRestingPosBlend = 0.22f;
                    batRightWingZ = batRestRPos.Z * batRestingPosBlend;
                    batLeftWingZ = batRestLPos.Z * batRestingPosBlend;
                }

                merged = BuildBat(
                    texRef,
                    profile,
                    isBaby,
                    rightWingYawRad: batRightRy,
                    leftWingYawRad: batLeftRy,
                    restingWingPivotZRight: batRightWingZ,
                    restingWingPivotZLeft: batLeftWingZ);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("blaze", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBlaze(texRef, profile, isBaby, rodSpin: idlePhase01 * 0.65f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (string.Equals(stem, "bee_stinger", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bee/bee_stinger", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeeStinger(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("bee", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBee(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.65f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("allay", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildAllay(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.70f + wave * 0.22f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("vex", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVex(
                    texRef,
                    profile,
                    isBaby,
                    yRotDegrees: animationTimeSeconds * 36f,
                    xRotDegrees: idlePhase01 * 8f + wave * 5f,
                    ageInTicks: animationTimeSeconds * 20f,
                    isCharging: false,
                    rightHandHoldingItem: false,
                    leftHandHoldingItem: false);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("phantom", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPhantom(normalizedAssetPath, texRef, profile, isBaby, flapTime: animationTimeSeconds);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("parrot", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildParrot(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.55f + wave * 0.22f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("happy_ghast_ropes", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/ghast/happy_ghast_ropes", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/equipment/happy_ghast_body/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHappyGhastHarness(texRef, profile, isBaby, gogglesEquippedBlend: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("happy_ghast", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/happy_ghast", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/ghast/happy_ghast", StringComparison.OrdinalIgnoreCase))
            {
                // Stem still matches substring "ghast" — branch before generic ghast so textures route correctly.
                merged = BuildHappyGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("ghast", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("guardian_elder", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("elder_guardian", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/guardian_elder", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 2.35f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("guardian_beam", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("guardian", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 1f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("pufferfish", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPufferfish(texRef, profile, isBaby, puff: 0.4f + idlePhase01 * 0.25f + wave * 0.12f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("turtle", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTurtle(texRef, profile, isBaby, swimLift: idlePhase01 * 0.20f + wave * 0.08f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("glow_squid", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("squid", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSquid(texRef, profile, isBaby, tentacleWave: idlePhase01 * 0.45f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("salmon", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSalmon(texRef, profile, isBaby, tailSway: idlePhase01 * 0.7f + wave * 0.22f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("cod", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildCod(texRef, profile, isBaby, tailSway: idlePhase01 * 0.8f + wave * 0.25f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("tropical_fish_b", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fish/tropical_fish_b", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTropicalFishB(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("tropical_fish_a", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fish/tropical_fish_a", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("tropical_fish", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/strider/", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, rawWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                var walkSpeed = MathF.Min(0.25f, rawWalkSpeed);
                merged = BuildStrider(
                    texRef,
                    profile,
                    isBaby,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    ageInTicks: animationTimeSeconds * 20f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/tadpole/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTadpole(texRef, profile, isBaby, tailSway: idlePhase01 * 0.45f + wave * 0.2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        // Path-based: stems like "frogspawn" contain "frog" but live under textures/block or unrelated dirs.
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/axolotl/", StringComparison.OrdinalIgnoreCase))
            {
                if (TryBuildAxolotlMeshPreferGeometryIr(
                        normalizedAssetPath,
                        stem,
                        texRef,
                        profile,
                        isBaby,
                        idlePhase01,
                        animationTimeSeconds,
                        out merged))
                {
                    specificRouteSlotHit = gpuBoneDispatchSlot;
                    return true;
                }

                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildAxolotl(
                texRef,
                profile,
                isBaby,
                idleBob: idlePhase01 * 0.12f + wave * 0.06f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/frog/", StringComparison.OrdinalIgnoreCase))
            {
                var frogCroak = idlePhase01 * 0.08f + wave * 0.05f;
                if (DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyPosition(profile, animationTimeSeconds, out var croakBodyPos))
                {
                    frogCroak = Math.Clamp(croakBodyPos.Y, 0f, 1f);
                }

                var frogLeftLegPitch = 0f;
                var frogRightLegPitch = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegRotationDegrees(profile, animationTimeSeconds, out var frogLLegDeg))
                {
                    frogLeftLegPitch = frogLLegDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegRotationDegrees(profile, animationTimeSeconds, out var frogRLegDeg))
                {
                    frogRightLegPitch = frogRLegDeg.X * (MathF.PI / 180f);
                }

                const float frogDegToRad = MathF.PI / 180f;
                var frogLArmX = 0f;
                var frogLArmY = 0f;
                var frogLArmZ = 0f;
                var frogRArmX = 0f;
                var frogRArmY = 0f;
                var frogRArmZ = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmRotationDegrees(profile, animationTimeSeconds, out var frogLArmDeg))
                {
                    frogLArmX = frogLArmDeg.X * frogDegToRad;
                    frogLArmY = frogLArmDeg.Y * frogDegToRad;
                    frogLArmZ = frogLArmDeg.Z * frogDegToRad;
                }

                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmRotationDegrees(profile, animationTimeSeconds, out var frogRArmDeg))
                {
                    frogRArmX = frogRArmDeg.X * frogDegToRad;
                    frogRArmY = frogRArmDeg.Y * frogDegToRad;
                    frogRArmZ = frogRArmDeg.Z * frogDegToRad;
                }

                var frogLArmPos = Vector3.Zero;
                var frogRArmPos = Vector3.Zero;
                var frogLLegPos = Vector3.Zero;
                var frogRLegPos = Vector3.Zero;
                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmPosition(profile, animationTimeSeconds, out var pLa))
                {
                    frogLArmPos = pLa;
                }

                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmPosition(profile, animationTimeSeconds, out var pRa))
                {
                    frogRArmPos = pRa;
                }

                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegPosition(profile, animationTimeSeconds, out var pLl))
                {
                    frogLLegPos = pLl;
                }

                if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegPosition(profile, animationTimeSeconds, out var pRl))
                {
                    frogRLegPos = pRl;
                }

                merged = BuildFrog(
                    texRef,
                    profile,
                    isBaby,
                    croakInflate: frogCroak,
                    walkLeftLegPitchRad: frogLeftLegPitch,
                    walkRightLegPitchRad: frogRightLegPitch,
                    walkLeftArmXRad: frogLArmX,
                    walkLeftArmYRad: frogLArmY,
                    walkLeftArmZRad: frogLArmZ,
                    walkRightArmXRad: frogRArmX,
                    walkRightArmYRad: frogRArmY,
                    walkRightArmZRad: frogRArmZ,
                    walkLeftArmPos: frogLArmPos,
                    walkRightArmPos: frogRArmPos,
                    walkLeftLegPos: frogLLegPos,
                    walkRightLegPos: frogRLegPos);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/signs/hanging/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHangingSignEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/signs/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildStandingSignEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/decorated_pot/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDecoratedPotEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/conduit/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildConduitEntity(texRef, profile, isBaby, spin: idlePhase01 * MathF.PI * 2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/creaking/", StringComparison.OrdinalIgnoreCase))
            {
                var creakLean = idlePhase01 * 0.12f + wave * 0.06f;
                if (DefinitionAnimationPreviewSampling.TrySampleCreakingWalkUpperBodyRotationDegrees(profile, animationTimeSeconds, out var upperBodyDeg))
                {
                    creakLean += upperBodyDeg.Z * (MathF.PI / 180f);
                }

                const float creakingAttackLoopSeconds = 0.708333f;
                var attackT = animationTimeSeconds % creakingAttackLoopSeconds;
                if (attackT < 0f)
                {
                    attackT += creakingAttackLoopSeconds;
                }

                if (DefinitionAnimationPreviewSampling.TrySampleCreakingAttackUpperBodyRotationDegrees(profile, attackT, out var attackUpperDeg))
                {
                    creakLean += attackUpperDeg.Y * (MathF.PI / 180f) * 0.02f;
                }

                merged = BuildCreaking(texRef, profile, isBaby, lean: creakLean);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("experience_orb", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/experience_orb", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildExperienceOrb(
                texRef,
                profile,
                isBaby,
                bob: idlePhase01 * 0.25f + wave * 0.1f,
                spritePick01: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("fishing_hook", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fishing_hook", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildFishingHook(texRef, profile, isBaby, sway: wave * 0.15f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("beacon_beam", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/beacon_beam", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeaconBeam(texRef, profile, isBaby, scroll: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/zombie_villager", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildZombieVillager(texRef, profile, isBaby, armLift: 1.15f + idlePhase01 * 0.55f + wave * 0.18f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/villager", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.18f + wave * 0.03f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("giant", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHumanoid(texRef, profile, isBaby, armLift: 0.35f + idlePhase01 * 0.25f + wave * 0.1f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/fish/", StringComparison.OrdinalIgnoreCase) &&
                stem.Contains("tropical_b", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTropicalFishB(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/fish/", StringComparison.OrdinalIgnoreCase) &&
                (stem.Contains("tropical_a", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("tropical_fish", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("end_gateway_beam", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_gateway_beam", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/skeleton/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.35f + wave * 0.12f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("end_portal", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_portal", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndPortalSurface(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/cat/", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildCat(
                texRef,
                profile,
                isBaby,
                headTilt: idlePhase01 * 0.2f + wave * 0.1f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("enchanting_table_book", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/enchanting_table_book", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnchantingTableBook(texRef, profile, isBaby, flap: idlePhase01 * 0.4f + wave * 0.15f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/copper_golem/", StringComparison.OrdinalIgnoreCase))
            {
                var golemSwing = idlePhase01 * 0.5f + wave * 0.2f;
                if (DefinitionAnimationPreviewSampling.TrySampleCopperGolemWalkBodyRotationDegrees(profile, animationTimeSeconds, out var golemBodyDeg))
                {
                    golemSwing += golemBodyDeg.X * (MathF.PI / 180f) * 0.25f;
                }

                merged = BuildCopperGolem(texRef, profile, isBaby, armSwing: golemSwing);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildChestEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/player/", StringComparison.OrdinalIgnoreCase) &&
                normalizedAssetPath.Contains("/textures/entity/player/slim/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPlayerSlim(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/player/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPlayerWide(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/llama/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLlama(texRef, profile, isBaby, neckBend: idlePhase01 * 0.30f + wave * 0.10f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        return false;
    }

    private static bool TryBuildForFamily(
        EntityRigFamily family,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel mergedModel)
    {
        mergedModel = null!;
        var wave = Wave(animationTimeSeconds, 0.7f);
        switch (family)
        {
            case EntityRigFamily.Humanoid:
                mergedModel = BuildHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.4f + wave * 0.1f);
                return true;
            case EntityRigFamily.Quadruped:
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                    mergedModel = BuildQuadruped(
                        texRef,
                        profile,
                        isBaby,
                        headPitch: idlePhase01 * 0.35f + wave * 0.1f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case EntityRigFamily.Flying:
                mergedModel = BuildFlying(texRef, profile, isBaby, wingSpread: 0.85f + idlePhase01 * 0.5f + wave * 0.2f);
                return true;
            case EntityRigFamily.Aquatic:
                mergedModel = BuildAquatic(texRef, profile, isBaby, tailSway: idlePhase01 * 0.6f + wave * 0.2f);
                return true;
            default:
                return false;
        }
    }

    private static EntityRigFamily ResolveFamily(string stem)
    {
        if (ContainsAny(stem, HumanoidKeys))
        {
            return EntityRigFamily.Humanoid;
        }

        if (ContainsAny(stem, QuadrupedKeys))
        {
            return EntityRigFamily.Quadruped;
        }

        if (ContainsAny(stem, FlyingKeys))
        {
            return EntityRigFamily.Flying;
        }

        if (ContainsAny(stem, AquaticKeys))
        {
            return EntityRigFamily.Aquatic;
        }

        return EntityRigFamily.Unknown;
    }
}
