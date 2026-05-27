using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Parity catalog and Geometry IR fast-path dispatch blocks for entity texture routing.</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// Cached parity-catalog route: Geometry IR mesh, suppress fallback, or hand-built catalog builder.
    /// </summary>
    private static bool TryDispatchParityCatalogCachedRoute(
        string norm,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        string cachedParityMethod,
        bool applyGeometryIrSetupAnimMotion,
        out EntityGpuBoneDispatchRoute discoveredRoute,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance,
        out bool stopDispatch)
    {
        discoveredRoute = default;
        mergedModel = null!;
        meshProvenance = default;
        stopDispatch = false;

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
            stopDispatch = true;
            return true;
        }

        if (cachedRule is not null &&
            ShouldSuppressHandBuiltParityFallback(profile, cachedRule, norm, stem, isBaby, out _))
        {
            mergedModel = null!;
            stopDispatch = true;
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
            stopDispatch = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Uncached parity-catalog route: resolves rule, Geometry IR mesh, suppress, or catalog builder; may end dispatch with failure.
    /// </summary>
    private static bool TryDispatchParityCatalogRoute(
        string norm,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        bool applyGeometryIrSetupAnimMotion,
        out EntityGpuBoneDispatchRoute discoveredRoute,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance)
    {
        discoveredRoute = default;
        mergedModel = null!;
        meshProvenance = default;

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

    private static EntityPreviewRouteKind? TryClassifyParityCatalogEntityTextureRoute(
        string norm,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds)
    {
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return null;
        }

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
}
