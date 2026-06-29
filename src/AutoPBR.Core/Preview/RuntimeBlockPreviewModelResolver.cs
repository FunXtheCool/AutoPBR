using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed class BlockPreviewResolveResult
{
    public MergedJavaBlockModel? MergedModel { get; init; }

    public List<string>? OrderedModelTextures { get; init; }

    public string? ModelDefaultNamespace { get; init; }

    public bool IsEmulatedEntityModel { get; init; }

    public PreviewMeshProvenance MeshProvenance { get; init; }

    public PreviewAssetSources AssetSources { get; init; } = null!;
}

internal static class RuntimeBlockPreviewModelResolver
{
    internal static BlockPreviewResolveResult Resolve(
        IAssetSource packSource,
        PreviewAssetSources assetSources,
        string archivePath,
        string extracted,
        MinecraftNativeProfile? previewNativeProfile,
        AutoPbrOptions options)
    {
        var result = new BlockPreviewResolveResult { AssetSources = assetSources };
        if (JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
                assetSources.Composite,
                archivePath,
                out var modelJsonPaths,
                out var ns) &&
            MinecraftModelMerger.TryMergeMany(assetSources.Composite, modelJsonPaths, out var merged))
        {
            var doorRebaked = BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(archivePath, ns, ref merged);
            var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, ns);
            if (ordered.Count > 0)
            {
                foreach (var asset in ordered)
                {
                    AssetSourceMaterializer.Materialize(assetSources.Composite, asset, extracted);
                }

                var primaryModelPath = modelJsonPaths[0];
                var origin = assetSources.ResolveModelJsonOrigin(primaryModelPath);
                var detail = PreviewAssetSources.FormatModelJsonDetail(
                    modelJsonPaths.Count > 1
                        ? $"{primaryModelPath} +{modelJsonPaths.Count - 1}"
                        : primaryModelPath,
                    origin);
                var provenance = new PreviewMeshProvenance(PreviewMeshDriverKind.PackModelJson, detail);
                if (doorRebaked)
                {
                    provenance = PreviewProvenanceFormatter.WithTag(provenance, "door-rebake");
                }

                return new BlockPreviewResolveResult
                {
                    MergedModel = merged,
                    OrderedModelTextures = ordered,
                    ModelDefaultNamespace = ns,
                    MeshProvenance = provenance,
                    AssetSources = assetSources,
                };
            }
        }

        if (VanillaBlockPreviewRuntime.IsBlockTextureArchivePath(archivePath) &&
            BlockTextureParityCatalog.IsCatalogued(archivePath) &&
            VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(
                archivePath,
                out var blockModel,
                out var blockProvenance,
                out var blockOrdered,
                out var blockNs))
        {
            foreach (var asset in blockOrdered)
            {
                if (assetSources.Composite.Exists(asset))
                {
                    AssetSourceMaterializer.Materialize(assetSources.Composite, asset, extracted);
                }
            }

            return new BlockPreviewResolveResult
            {
                MergedModel = blockModel,
                OrderedModelTextures = blockOrdered,
                ModelDefaultNamespace = blockNs,
                MeshProvenance = blockProvenance,
                AssetSources = assetSources,
            };
        }

        if (IsEntityTextureArchivePath(archivePath))
        {
            var runtime = EntityModelRuntimeFactory.Create();
            var profile = ResolvePreviewMeshNativeProfile(previewNativeProfile);
            var idlePhase = ComputeDeterministicIdlePhase(archivePath, profile.Name);
            var animTime = ComputeDeterministicAnimationTimeSeconds(archivePath, profile.Name);
            if (runtime.TryBuildStaticMesh(
                    archivePath,
                    profile,
                    idlePhase,
                    animTime,
                    out var emuModel,
                    out var emuProvenance,
                    applyGeometryIrSetupAnimMotion: false))
            {
                var entityNs = TryGetAssetNamespace(archivePath) ?? "minecraft";
                var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(emuModel, entityNs);
                if (ordered.Count > 0)
                {
                    foreach (var asset in ordered)
                    {
                        AssetSourceMaterializer.Materialize(assetSources.Composite, asset, extracted);
                    }

                    return new BlockPreviewResolveResult
                    {
                        MergedModel = emuModel,
                        OrderedModelTextures = ordered,
                        ModelDefaultNamespace = entityNs,
                        IsEmulatedEntityModel = true,
                        MeshProvenance = emuProvenance,
                        AssetSources = assetSources,
                    };
                }
            }
        }

        return result;
    }

    private static bool IsEntityTextureArchivePath(string archivePath) =>
        archivePath.Replace('\\', '/').Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetAssetNamespace(string archivePath)
    {
        var parts = archivePath.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
    }

    private static MinecraftNativeProfile ResolvePreviewMeshNativeProfile(MinecraftNativeProfile? resolved)
    {
        if (resolved is { Name: var n } && NativeIrVersionLabels.IsRecognizedProfileName(n))
        {
            return resolved;
        }

        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        return MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot)
               ?? MinecraftNativeProfileResolver.ResolveAutoLatest(nativeRoot)
               ?? new MinecraftNativeProfile(
                   NativeIrVersionLabels.ModernGeometryLabel,
                   nativeRoot,
                   new Version(26, 1, 2));
    }

    private static float ComputeDeterministicIdlePhase(string archivePath, string profileName)
    {
        var s = archivePath + "|" + profileName;
        unchecked
        {
            var h = 17;
            foreach (var ch in s)
            {
                h = (h * 31) + ch;
            }

            return ((h & 0x7fffffff) % 1000) / 1000f;
        }
    }

    private static float ComputeDeterministicAnimationTimeSeconds(string archivePath, string profileName)
    {
        var s = archivePath + "|anim|" + profileName;
        unchecked
        {
            var h = 19;
            foreach (var ch in s)
            {
                h = (h * 31) + ch;
            }

            return ((h & 0x7fffffff) % 120_000) / 1000f;
        }
    }
}
