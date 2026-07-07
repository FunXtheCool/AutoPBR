// ReSharper disable CheckNamespace

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Entity texture dispatch: parity-catalog geometry IR only; failures surface as error placeholder upstream.
/// </summary>
internal sealed partial class EntityModelRuntime
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

        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return false;
        }

        if (EntityTextureParityCatalog.ResolveRule(norm, stem) is not { } parityRule)
        {
            return false;
        }

        if (routeCache is { Kind: EntityGpuBoneDispatchKind.ParityCatalog, ParityBuilderMethod: var cachedParityMethod } &&
            !string.IsNullOrEmpty(cachedParityMethod) &&
            string.Equals(cachedParityMethod, parityRule.BuilderMethod, StringComparison.Ordinal) &&
            TryBuildParityCatalogMeshFromGeometryIr(
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
                out var cachedIrJvm))
        {
            discoveredRoute = EntityGpuBoneDispatchRoute.ForParity(cachedParityMethod, cachedIrJvm);
            meshProvenance = new(PreviewMeshDriverKind.RuntimeGeometryIrJson, cachedIrJvm);
            return true;
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

        return false;
    }

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
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return EntityPreviewRouteKind.ErrorPlaceholder;
        }

        if (EntityTextureParityCatalog.ResolveRule(norm, stem) is not { } parityRule)
        {
            return EntityPreviewRouteKind.ErrorPlaceholder;
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
            return EntityPreviewRouteKind.ParityCatalogGeometryIr;
        }

        return EntityPreviewRouteKind.ErrorPlaceholder;
    }
}
