using AutoPBR.Core.Models;
using System.Text.Json;

namespace AutoPBR.Preview;

internal static class EntityGeometryIrTextureAtlas
{
    /// <summary>
    /// Decorated pot preview uses two atlases: <c>#base</c> neck/caps (32×32) and <c>#skin</c> side sheets (16×16).
    /// Manifest rows still carry placeholder 64×64; javap <c>createBaseLayer</c>/<c>createSidesLayer</c> use the smaller sizes.
    /// </summary>
    private static bool TryResolveDecoratedPotBakeAtlas(string normalizedTexturePath, out (int Width, int Height) size)
    {
        size = default;
        var path = normalizedTexturePath.Replace('\\', '/').TrimStart('/');
        if (!path.Contains("/textures/entity/decorated_pot/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
        {
            size = (32, 32);
            return true;
        }

        // Pattern sprites and side template (north-only texOffs 1,0 on 16×16).
        size = (16, 16);
        return true;
    }

    /// <summary>
    /// Atlas dimensions for UV normalization during mesh bake. Prefer lifted geometry-IR shard
    /// <c>textureWidth</c>/<c>textureHeight</c> (matches emit) over manifest placeholders; physical PNG size is upload-only.
    /// </summary>
    public static (int Width, int Height) ResolveForBake(
        string normalizedTexturePath,
        int physicalWidth,
        int physicalHeight,
        PreviewMeshProvenance provenance,
        MinecraftNativeProfile? nativeProfile = null)
    {
        if (TryResolveDecoratedPotBakeAtlas(normalizedTexturePath, out var decoratedPot))
        {
            return decoratedPot;
        }

        if (TryResolveSupplementaryLayerBakeAtlas(normalizedTexturePath, out var supplementaryLayer))
        {
            return supplementaryLayer;
        }

        if (provenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson &&
            !string.IsNullOrWhiteSpace(provenance.Detail) &&
            nativeProfile is not null &&
            GeometryIrDocumentLoader.TryLoadLiftedForParityCatalog(nativeProfile, provenance.Detail, out var geometryRoot) &&
            TryReadShardAtlas(geometryRoot, out var shardW, out var shardH))
        {
            return (shardW, shardH);
        }

        if (provenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson &&
            TryResolveManifestGeometryIrBakeAtlas(normalizedTexturePath, out var manifestAtlas))
        {
            return manifestAtlas;
        }

        if (provenance.Kind != PreviewMeshDriverKind.RuntimeGeometryIrJson)
        {
            return (physicalWidth, physicalHeight);
        }

        var stem = Path.GetFileNameWithoutExtension(normalizedTexturePath);
        var rule = EntityTextureParityCatalog.ResolveRule(normalizedTexturePath, stem);
        if (rule is not null &&
            GeometryIrParityAtlasDefaults.TryGetForBuilderMethod(
                rule.BuilderMethod,
                out var width,
                out var height))
        {
            return (width, height);
        }

        return (physicalWidth, physicalHeight);
    }

    private static bool TryResolveManifestGeometryIrBakeAtlas(
        string normalizedTexturePath,
        out (int Width, int Height) size)
    {
        size = default;
        var path = normalizedTexturePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(path);
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        if (rule is null ||
            rule.GeometryIrTextureWidth is not int manifestW ||
            manifestW <= 0 ||
            rule.GeometryIrTextureHeight is not int manifestH ||
            manifestH <= 0)
        {
            return false;
        }

        size = (manifestW, manifestH);
        return true;
    }

    /// <summary>
    /// Multi-<c>LayerDefinition</c> models (e.g. Breeze <c>createWindLayer</c> 128²) keep the primary factory
    /// atlas on the shard root (32²) while supplementary PNG paths declare their own logical sheet in manifest.
    /// </summary>
    private static bool TryResolveSupplementaryLayerBakeAtlas(
        string normalizedTexturePath,
        out (int Width, int Height) size)
    {
        size = default;
        var path = normalizedTexturePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(path);
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        if (rule is null ||
            rule.GeometryIrTextureWidth is not > 0 ||
            rule.GeometryIrTextureHeight is not > 0)
        {
            return false;
        }

        var width = rule.GeometryIrTextureWidth!.Value;
        var height = rule.GeometryIrTextureHeight!.Value;

        if (string.Equals(rule.BuilderMethod, "Breeze", StringComparison.Ordinal) &&
            path.Contains("/breeze_wind", StringComparison.OrdinalIgnoreCase))
        {
            size = (width, height);
            return true;
        }

        return false;
    }

    internal static bool TryReadShardAtlas(JsonElement geometryRoot, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!geometryRoot.TryGetProperty("textureWidth", out var tw) ||
            tw.ValueKind != JsonValueKind.Number ||
            !tw.TryGetInt32(out var shardW) ||
            shardW <= 0 ||
            !geometryRoot.TryGetProperty("textureHeight", out var th) ||
            th.ValueKind != JsonValueKind.Number ||
            !th.TryGetInt32(out var shardH) ||
            shardH <= 0)
        {
            return false;
        }

        width = shardW;
        height = shardH;
        return true;
    }
}
