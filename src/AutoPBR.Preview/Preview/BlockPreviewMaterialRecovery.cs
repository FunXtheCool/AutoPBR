using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

internal static class BlockPreviewMaterialRecovery
{
    internal static bool TryRecoverBlockPreviewMaterials(
        PreviewAssetSources assetSources,
        string archivePath,
        string extracted,
        MergedJavaBlockModel mergedModel,
        string modelDefaultNs,
        ref List<string> orderedModelTextures,
        ref IReadOnlyList<TextureWorkItem> textures,
        AutoPBROptions options,
        CancellationToken cancellationToken)
    {
        if (mergedModel.Elements.Count == 0)
        {
            return false;
        }

        orderedModelTextures = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedModel, modelDefaultNs);
        if (orderedModelTextures.Count == 0)
        {
            return false;
        }

        MaterializeMissingTextures(assetSources.Composite, orderedModelTextures, extracted);
        TrySubstituteMissingTextureReferences(
            assetSources.Composite,
            archivePath,
            mergedModel,
            modelDefaultNs,
            orderedModelTextures,
            extracted);

        orderedModelTextures = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedModel, modelDefaultNs);
        if (orderedModelTextures.Count == 0)
        {
            return false;
        }

        MaterializeMissingTextures(assetSources.Composite, orderedModelTextures, extracted);
        textures = TextureScanner.ScanTextures(
            extracted,
            options,
            applyFoliageIgnoreFilter: false,
            cancellationToken: cancellationToken);

        var workOrdered = new List<TextureWorkItem>(orderedModelTextures.Count);
        foreach (var zpath in orderedModelTextures)
        {
            var w = JavaModelPreviewPipeline.FindWorkItemByDiffuseZipPath(textures, extracted, zpath);
            if (w is null)
            {
                return false;
            }

            workOrdered.Add(w);
        }

        return workOrdered.Count == orderedModelTextures.Count;
    }

    private static void MaterializeMissingTextures(
        IAssetSource source,
        IReadOnlyList<string> orderedModelTextures,
        string extracted)
    {
        foreach (var zpath in orderedModelTextures)
        {
            if (!ArchivePathSafety.TryResolveExtractionPath(extracted, zpath, out var outPath) ||
                File.Exists(outPath))
            {
                continue;
            }

            AssetSourceMaterializer.Materialize(source, zpath, extracted);
        }
    }

    private static void TrySubstituteMissingTextureReferences(
        IAssetSource source,
        string archivePath,
        MergedJavaBlockModel mergedModel,
        string modelDefaultNs,
        List<string> orderedModelTextures,
        string extracted)
    {
        if (!VanillaBlockPreviewRuntime.IsBlockTextureArchivePath(archivePath))
        {
            return;
        }

        var rule = BlockTextureParityCatalog.ResolveRule(archivePath);
        if (rule?.TextureSlots is null || rule.TextureSlots.Count == 0)
        {
            return;
        }

        if (!BlockTextureSlotResolver.TryResolveSlotZipPaths(rule, archivePath, modelDefaultNs, out var slotToZipPath))
        {
            return;
        }

        var missing = orderedModelTextures
            .Where(z => !TextureExistsOnDisk(extracted, z))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var (face, slotStem) in rule.TextureSlots)
        {
            var candidate = slotToZipPath.TryGetValue(face, out var zipPath)
                ? zipPath
                : BlockTextureSlotResolver.StemToBlockTextureZipPath(modelDefaultNs, slotStem);
            if (!source.Exists(candidate))
            {
                continue;
            }

            foreach (var missingPath in missing)
            {
                if (TextureExistsOnDisk(extracted, missingPath))
                {
                    continue;
                }

                var missingStem = Path.GetFileNameWithoutExtension(missingPath);
                if (string.Equals(missingStem, slotStem, StringComparison.OrdinalIgnoreCase) ||
                    missingPath.Contains(slotStem, StringComparison.OrdinalIgnoreCase))
                {
                    AssetSourceMaterializer.Materialize(source, candidate, extracted);
                }
            }
        }
    }

    private static bool TextureExistsOnDisk(string extracted, string zipPath) =>
        ArchivePathSafety.TryResolveExtractionPath(extracted, zipPath, out var outPath) && File.Exists(outPath);
}
