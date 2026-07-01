using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static class VanillaBlockPreviewRuntime
{
    public static bool TryBuildSyntheticMesh(
        string normalizedBlockTexturePath,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance,
        out List<string> orderedTextureZipPaths,
        out string defaultNamespace)
    {
        mergedModel = null!;
        meshProvenance = default;
        orderedTextureZipPaths = [];
        defaultNamespace = TryGetAssetNamespace(normalizedBlockTexturePath) ?? "minecraft";

        var rule = BlockTextureParityCatalog.ResolveRule(normalizedBlockTexturePath);
        if (rule is null || !rule.CanSynthesizePreview())
        {
            return false;
        }

        if (!BlockTextureSlotResolver.TryResolveSlotZipPaths(
                rule,
                normalizedBlockTexturePath,
                defaultNamespace,
                out var slotToZipPath))
        {
            return false;
        }

        if (string.Equals(rule.FamilyId, "grass_block", StringComparison.OrdinalIgnoreCase))
        {
            VanillaBlockGrassCubeBuilder.AddOverlaySlot(slotToZipPath, defaultNamespace);
        }

        mergedModel = rule.PreviewShape switch
        {
            BlockTextureParityPreviewShape.UniformCube or BlockTextureParityPreviewShape.CubeDirectional
                or BlockTextureParityPreviewShape.CubeColumnY => BuildCube(rule, slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.ThinPlate => BuildThinPlate(normalizedBlockTexturePath, slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.DoorHalf => BuildDoorHalf(normalizedBlockTexturePath, slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.CakeWedge => BuildCake(slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.CakeSlice => BuildCakeSlice(slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.CactusCross => BuildCactus(slotToZipPath, defaultNamespace),
            BlockTextureParityPreviewShape.FencePost => BuildTextureShape(
                normalizedBlockTexturePath, slotToZipPath, defaultNamespace, VanillaBlockFencePostBuilder.BuildPostOnly),
            BlockTextureParityPreviewShape.FenceWithLink => BuildTextureShape(
                normalizedBlockTexturePath, slotToZipPath, defaultNamespace, VanillaBlockFencePostBuilder.BuildWithNorthLink),
            BlockTextureParityPreviewShape.RailTrack => BuildTextureShape(
                normalizedBlockTexturePath, slotToZipPath, defaultNamespace, VanillaBlockRailTrackBuilder.Build),
            BlockTextureParityPreviewShape.StairWedge => BuildTextureShape(
                normalizedBlockTexturePath, slotToZipPath, defaultNamespace, VanillaBlockStairWedgeBuilder.Build),
            BlockTextureParityPreviewShape.CrossSprite => BuildTextureShape(
                normalizedBlockTexturePath, slotToZipPath, defaultNamespace, VanillaBlockCrossSpriteBuilder.Build),
            _ => null!,
        };

        if (mergedModel is null || mergedModel.Elements.Count == 0)
        {
            return false;
        }

        if (string.Equals(rule.FamilyId, "grass_block_snow", StringComparison.OrdinalIgnoreCase))
        {
            var snowCapped = BlockGrassSnowPreviewPairing.TryAppendSnowCapForGrassBlockSnow(
                normalizedBlockTexturePath,
                defaultNamespace,
                ref mergedModel);
            orderedTextureZipPaths =
                JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedModel, defaultNamespace);
            if (snowCapped)
            {
                meshProvenance = PreviewProvenanceFormatter.WithTag(
                    PreviewProvenanceFormatter.WithTag(
                        new PreviewMeshProvenance(PreviewMeshDriverKind.VanillaBlockParity, rule.PreviewShape.ToString()),
                        "parity-synthesis"),
                    "snow-cap");
            }
            else
            {
                meshProvenance = PreviewProvenanceFormatter.WithTag(
                    new PreviewMeshProvenance(PreviewMeshDriverKind.VanillaBlockParity, rule.PreviewShape.ToString()),
                    "parity-synthesis");
            }
        }
        else
        {
            orderedTextureZipPaths = BlockTextureSlotResolver.CollectOrderedDistinctZipPaths(slotToZipPath);
            meshProvenance = PreviewProvenanceFormatter.WithTag(
                new PreviewMeshProvenance(PreviewMeshDriverKind.VanillaBlockParity, rule.PreviewShape.ToString()),
                "parity-synthesis");
        }

        if (orderedTextureZipPaths.Count == 0)
        {
            return false;
        }

        return true;
    }

    public static bool IsBlockTextureArchivePath(string archivePath) =>
        archivePath.Replace('\\', '/').Contains("/textures/block/", StringComparison.OrdinalIgnoreCase);

    private static MergedJavaBlockModel BuildCube(
        BlockTextureParityRule rule,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        if (string.Equals(rule.FamilyId, "grass_block", StringComparison.OrdinalIgnoreCase))
        {
            return VanillaBlockGrassCubeBuilder.Build(rule, slotToZipPath, defaultNamespace);
        }

        var textures = BlockTextureSlotResolver.BuildTextureDictionary(rule, slotToZipPath, defaultNamespace);
        var faceKeys = VanillaBlockCubeBuilder.BuildFaceTextureKeys(slotToZipPath);
        return VanillaBlockCubeBuilder.Build(faceKeys, textures);
    }

    private static MergedJavaBlockModel BuildThinPlate(
        string selectedPath,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["texture"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(
                slotToZipPath.TryGetValue("texture", out var tex)
                    ? tex
                    : selectedPath,
                defaultNamespace),
        };
        return VanillaBlockThinPlateBuilder.Build("texture", textures);
    }

    private static MergedJavaBlockModel BuildDoorHalf(
        string selectedPath,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var bottomZip = slotToZipPath.TryGetValue("bottom", out var b)
            ? b
            : slotToZipPath.Values.FirstOrDefault(v => v.Contains("_bottom", StringComparison.OrdinalIgnoreCase)) ??
              selectedPath;
        var topZip = slotToZipPath.TryGetValue("top", out var t)
            ? t
            : slotToZipPath.Values.FirstOrDefault(v => v.Contains("_top", StringComparison.OrdinalIgnoreCase)) ??
              selectedPath;
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bottom"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(bottomZip, defaultNamespace),
            ["top"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(topZip, defaultNamespace),
        };
        return VanillaBlockDoorHalfBuilder.BuildPair(textures);
    }

    private static MergedJavaBlockModel BuildCake(
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var bottomZip = slotToZipPath.GetValueOrDefault("down") ??
                        slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_bottom", StringComparison.OrdinalIgnoreCase)) ??
                        slotToZipPath.Values.First();
        var sideZip = slotToZipPath.GetValueOrDefault("north") ??
                      slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_side", StringComparison.OrdinalIgnoreCase)) ??
                      slotToZipPath.Values.First();
        var topZip = slotToZipPath.GetValueOrDefault("up") ??
                     slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_top", StringComparison.OrdinalIgnoreCase)) ??
                     slotToZipPath.Values.First();
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bottom"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(bottomZip, defaultNamespace),
            ["side"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(sideZip, defaultNamespace),
            ["top"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(topZip, defaultNamespace),
        };
        return VanillaBlockCakeBuilder.Build(textures);
    }

    private static MergedJavaBlockModel BuildCakeSlice(
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var bottomZip = slotToZipPath.GetValueOrDefault("down") ??
                        slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_bottom", StringComparison.OrdinalIgnoreCase)) ??
                        slotToZipPath.Values.First();
        var sideZip = slotToZipPath.GetValueOrDefault("north") ??
                      slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_side", StringComparison.OrdinalIgnoreCase)) ??
                      slotToZipPath.Values.First();
        var topZip = slotToZipPath.GetValueOrDefault("up") ??
                     slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_top", StringComparison.OrdinalIgnoreCase)) ??
                     slotToZipPath.Values.First();
        var insideZip = slotToZipPath.GetValueOrDefault("inside") ??
                        slotToZipPath.GetValueOrDefault("west") ??
                        slotToZipPath.Values.FirstOrDefault(v => v.Contains("cake_inner", StringComparison.OrdinalIgnoreCase)) ??
                        sideZip;
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bottom"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(bottomZip, defaultNamespace),
            ["side"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(sideZip, defaultNamespace),
            ["top"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(topZip, defaultNamespace),
            ["inside"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(insideZip, defaultNamespace),
        };
        return VanillaBlockCakeSliceBuilder.Build(textures);
    }

    private static MergedJavaBlockModel BuildCactus(
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        string Ref(string slot, string fallbackStem) =>
            BlockTextureSlotResolver.ZipPathToModelTextureReference(
                slotToZipPath.TryGetValue(slot, out var zip)
                    ? zip
                    : slotToZipPath.Values.FirstOrDefault(v =>
                          v.Contains(fallbackStem, StringComparison.OrdinalIgnoreCase)) ??
                  slotToZipPath.Values.First(),
                defaultNamespace);

        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bottom"] = Ref("down", "cactus_bottom"),
            ["top"] = Ref("up", "cactus_top"),
            ["side"] = Ref("north", "cactus_side"),
        };
        return VanillaBlockCactusBuilder.Build(textures);
    }

    private static MergedJavaBlockModel BuildTextureShape(
        string selectedPath,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace,
        Func<string, IReadOnlyDictionary<string, string>, MergedJavaBlockModel> build)
    {
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["texture"] = BlockTextureSlotResolver.ZipPathToModelTextureReference(
                slotToZipPath.TryGetValue("texture", out var tex) ? tex : selectedPath,
                defaultNamespace),
        };
        return build("texture", textures);
    }

    private static string? TryGetAssetNamespace(string archivePath)
    {
        var parts = archivePath.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
    }
}
