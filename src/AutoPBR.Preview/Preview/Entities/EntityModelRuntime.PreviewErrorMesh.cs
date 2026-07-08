// ReSharper disable CheckNamespace
using AutoPBR.Core.Models;

namespace AutoPBR.Preview.Entities;

/// <summary>Source-style 3D exclamation placeholder when entity geometry cannot be resolved.</summary>
internal sealed partial class EntityModelRuntime
{
    internal static class EntityPreviewErrorMeshReasons
    {
        internal const string UncataloguedEntityTexture = "uncatalogued_entity_texture";
        internal const string CatalogRuleMissing = "catalog_rule_missing";
    }

    internal static bool TryBuildErrorPlaceholderMesh(
        string normalizedAssetPath,
        string reasonCode,
        out MergedJavaBlockModel mesh,
        out PreviewMeshProvenance provenance)
    {
        mesh = null!;
        provenance = default;
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var texRef = ToTextureRef(norm);
        var b = new RigBuilder(16, 16);
        // Vertical bar of "!" in model texel space (LER column-root applied below).
        b.AddBox(-1f, 11f, -0.5f, 1f, 21f, 0.5f, "skin", 1f, 0f, 0f, 0f, texU: 0, texV: 0);
        b.AddBox(-1.5f, 4f, -0.5f, 1.5f, 8f, 0.5f, "skin", 1f, 0f, 0f, 0f, texU: 0, texV: 0);
        mesh = ApplyLivingEntityRendererColumnRootScale(b.Build(texRef));
        provenance = new PreviewMeshProvenance(PreviewMeshDriverKind.ErrorPlaceholder, reasonCode);
        return true;
    }

    internal static string ResolveEntityMeshFailureReason(
        string normalizedAssetPath,
        string stem,
        MinecraftNativeProfile profile,
        bool isBaby)
    {
        if (!EntityTextureParityCatalog.IsCatalogued(normalizedAssetPath))
        {
            return EntityPreviewErrorMeshReasons.UncataloguedEntityTexture;
        }

        var rule = EntityTextureParityCatalog.ResolveRule(normalizedAssetPath, stem);
        if (rule is null)
        {
            return EntityPreviewErrorMeshReasons.CatalogRuleMissing;
        }

        if (ShouldSuppressHandBuiltParityFallback(profile, rule, normalizedAssetPath, stem, isBaby, out var irReason))
        {
            return irReason;
        }

        return ClassifyParityCatalogGeometryIrFailure(profile, rule, normalizedAssetPath, stem, isBaby);
    }
}
