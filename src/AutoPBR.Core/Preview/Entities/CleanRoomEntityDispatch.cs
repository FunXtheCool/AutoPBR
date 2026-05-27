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
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(cachedParityMethod, irJvm);
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
                discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(parityRule.BuilderMethod, irJvm);
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
