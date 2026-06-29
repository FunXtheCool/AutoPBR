using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static class ArchiveModelInventoryBuilder
{
    internal static ArchiveModelInventory BuildFromArchivePaths(IEnumerable<string> archivePaths)
    {
        var modelJson = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blockTextures = new List<string>();
        foreach (var path in archivePaths)
        {
            var norm = path.Replace('\\', '/').TrimStart('/');
            if (norm.Contains("/models/block/", StringComparison.OrdinalIgnoreCase) &&
                norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                modelJson.Add(norm);
                continue;
            }

            if (norm.Contains("/textures/block/", StringComparison.OrdinalIgnoreCase) &&
                norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                blockTextures.Add(norm);
            }
        }

        var coverage = new Dictionary<string, BlockPreviewCoverageKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var tex in blockTextures)
        {
            coverage[tex] = ClassifyBlockTexture(tex, modelJson);
        }

        return new ArchiveModelInventory(modelJson, coverage);
    }

    private static BlockPreviewCoverageKind ClassifyBlockTexture(string texturePath, IReadOnlySet<string> modelJson)
    {
        var rule = BlockTextureParityCatalog.ResolveRule(texturePath);
        if (rule is not null && rule.CanSynthesizePreview())
        {
            return BlockPreviewCoverageKind.ParityCatalogSynthesizable;
        }

        if (rule?.PreviewShape == BlockTextureParityPreviewShape.PackModelJsonOnly)
        {
            return BlockPreviewCoverageKind.PackModelJsonOnly;
        }

        var stem = Path.GetFileNameWithoutExtension(texturePath);
        var candidates = new[]
        {
            $"assets/minecraft/models/block/{JavaModelPathResolver.MapTextureStemToModelStem(stem)}.json",
            $"assets/minecraft/models/block/{stem}.json",
        };
        if (candidates.Any(modelJson.Contains))
        {
            return BlockPreviewCoverageKind.HasPackModelJson;
        }

        return BlockPreviewCoverageKind.Unknown;
    }
}
