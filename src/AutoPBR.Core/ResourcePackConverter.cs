using System.IO.Compression;
using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core;

public static class ResourcePackConverter
{
    public static async Task ConvertAsync(
        string inputZipPath,
        string outputZipPath,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputZipPath))
        {

            throw new FileNotFoundException("Input pack not found.", inputZipPath);
        }


        if (options.SpecularData is null)
        {

            throw new InvalidOperationException("SpecularData is required (load textures_data.json first).");
        }


        var baseTemp = string.IsNullOrWhiteSpace(options.TempDirectory)
            ? Path.GetTempPath()
            : options.TempDirectory;
        var tempRoot = Path.Combine(baseTemp, "AutoPBR", Guid.NewGuid().ToString("N"));
        var extracted = Path.Combine(tempRoot, "pack_unzipped");
        Directory.CreateDirectory(extracted);

        try
        {
            await Task.Run(() =>
            {
                if (options.UseLegacyExtractor)
                {
                    PackExtractionService.ExtractPack(inputZipPath, extracted, options, progress, cancellationToken);
                }
                else
                {

                    ParallelZipReader.ExtractZip(inputZipPath, extracted, options, progress, ConversionStage.Extracting,
                        cancellationToken, options.EntriesToExtractOnly);
                }

            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // ScanTextures enumerates PNGs and resolves per-texture material/flag tags (keyword + MiniLM + post-process + manual overrides).
            // This must finish before conversion so brick/ore/organic rules apply to TextureWorkItem.Overrides.
            progress?.Report(new ConversionProgress(ConversionStage.ScanningTextures, 0, 1));
            var textures = TextureScanner.ScanTextures(
                extracted,
                options,
                progress,
                cachePackPath: inputZipPath,
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Normals/height before specular so brick mortar probe can cache invert specular R (TextureOverrides.BrickProbeAppliedGlobalInvert).
            await TextureConversionPipeline.GenerateNormalsAndSpecularAsync(textures, options, progress, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            PackWriter.WritePackMetadata(extracted);

            Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath) ?? ".");
            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }


            await Task.Run(() => PackWriter.CreateOutputZip(extracted, outputZipPath, textures, options, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new ConversionProgress(ConversionStage.Done, 0, 0));
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    /// <summary>
    /// Build a 2D composite preview (diffuse, normal, specular, height) for a single texture from the pack.
    /// Returns a PNG-encoded byte array suitable for UI display.
    /// </summary>
    public static async Task<PreviewRenderResult> RenderPreviewAsync(
        string inputZipPath,
        string archivePath,
        AutoPbrOptions options,
        CancellationToken cancellationToken = default)
    {
        var detailed = await RenderPreviewDetailedAsync(inputZipPath, archivePath, options, cancellationToken)
            .ConfigureAwait(false);
        return new PreviewRenderResult(detailed.PngBytes, detailed.BrickProbeDebugText);
    }

    /// <summary>
    /// Same pipeline as <see cref="RenderPreviewAsync"/> but also returns raw RGBA maps for 3D preview before temp cleanup.
    /// </summary>
    public static async Task<PreviewDetailedResult> RenderPreviewDetailedAsync(
        string inputZipPath,
        string archivePath,
        AutoPbrOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputZipPath))
        {
            throw new FileNotFoundException("Input pack not found.", inputZipPath);
        }

        if (options.SpecularData is null)
        {
            throw new InvalidOperationException("SpecularData is required (load textures_data.json first).");
        }

        var baseTemp = string.IsNullOrWhiteSpace(options.TempDirectory)
            ? Path.GetTempPath()
            : options.TempDirectory;
        var tempRoot = Path.Combine(baseTemp, "AutoPBR_Preview", Guid.NewGuid().ToString("N"));
        var extracted = Path.Combine(tempRoot, "pack_unzipped");
        Directory.CreateDirectory(extracted);

        try
        {
            PackExtractionService.ExtractEntry(inputZipPath, archivePath, extracted);

            cancellationToken.ThrowIfCancellationRequested();

            var previewNativeProfile = ResolveNativeMinecraftDataProfile(inputZipPath, extracted);
            MergedJavaBlockModel? mergedModel = null;
            List<string>? orderedModelTextures = null;
            string? modelDefaultNs = null;
            var isEmulatedEntityModel = false;
            PreviewMeshProvenance meshProvenance = default;
            using (var zip = ZipFile.OpenRead(inputZipPath))
            {
                var zipSource = new ZipAssetSource(zip);
                var nativeProfile = previewNativeProfile;
                var nativeRoot = nativeProfile?.RootDirectory;
                var previewAssetSource = nativeRoot is not null && Directory.Exists(nativeRoot)
                    ? new CompositeAssetSource(zipSource, new DirectoryAssetSource(nativeRoot))
                    : new CompositeAssetSource(zipSource);
                if (JavaModelPathResolver.TryResolveModelJsonFromTexture(previewAssetSource, archivePath, out var modelJsonPath,
                        out var ns) &&
                    MinecraftModelMerger.TryMerge(previewAssetSource, modelJsonPath, out var merged))
                {
                    var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, ns);
                    if (ordered.Count > 0)
                    {
                        foreach (var asset in ordered)
                        {
                            AssetSourceMaterializer.Materialize(previewAssetSource, asset, extracted);
                        }

                        mergedModel = merged;
                        orderedModelTextures = ordered;
                        modelDefaultNs = ns;
                        meshProvenance = new(PreviewMeshDriverKind.PackModelJson, modelJsonPath);
                    }
                }

                // Vanilla entities are often code-driven; if no JSON model exists, use clean-room runtime fallback.
                if (mergedModel is null && IsEntityTextureArchivePath(archivePath))
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
                            out meshProvenance,
                            applyGeometryIrSetupAnimMotion: false))
                    {
                        var entityNs = TryGetAssetNamespace(archivePath) ?? "minecraft";
                        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(emuModel, entityNs);
                        if (ordered.Count > 0)
                        {
                            foreach (var asset in ordered)
                            {
                                AssetSourceMaterializer.Materialize(previewAssetSource, asset, extracted);
                            }

                            mergedModel = emuModel;
                            orderedModelTextures = ordered;
                            modelDefaultNs = entityNs;
                            isEmulatedEntityModel = true;
                        }
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var textures = TextureScanner.ScanTextures(
                extracted,
                options,
                cachePackPath: inputZipPath,
                cancellationToken: cancellationToken);
            if (textures.Count == 0)
            {
                throw new InvalidOperationException("No previewable textures found after extraction.");
            }

            TextureWorkItem target = textures[0];
            var targetRel = archivePath.Replace('\\', '/');
            foreach (var t in textures)
            {
                var rel = Path.GetRelativePath(extracted, t.DiffusePath).Replace('\\', '/');
                if (string.Equals(rel, targetRel, StringComparison.OrdinalIgnoreCase))
                {
                    target = t;
                    break;
                }
            }

            if (mergedModel is not null && orderedModelTextures is not null && modelDefaultNs is not null)
            {
                var workOrdered = new List<TextureWorkItem>(orderedModelTextures.Count);
                foreach (var zpath in orderedModelTextures)
                {
                    var w = JavaModelPreviewPipeline.FindWorkItemByDiffuseZipPath(textures, extracted, zpath);
                    if (w is null)
                    {
                        workOrdered = null;
                        break;
                    }

                    workOrdered.Add(w);
                }

                if (workOrdered is not null)
                {
                    await NormalHeightGenerator.GenerateAsync(workOrdered, options, null, cancellationToken)
                        .ConfigureAwait(false);
                    await SpecularGenerator.GenerateAsync(workOrdered, options, null, cancellationToken)
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    var materials = workOrdered.Select(PreviewTextureMapsLoader.Load).ToArray();
                    var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < orderedModelTextures.Count; i++)
                    {
                        texSizes[orderedModelTextures[i]] = (materials[i].Width, materials[i].Height);
                    }

                    var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < orderedModelTextures.Count; i++)
                    {
                        pathToIdx[orderedModelTextures[i]] = i;
                    }

                    if (MinecraftModelBaker.TryBake(mergedModel, modelDefaultNs, pathToIdx, texSizes, out var verts,
                            out var indices, out var batchesList))
                    {
                        var normSel = archivePath.Replace('\\', '/').TrimStart('/');
                        var primIdx = orderedModelTextures.FindIndex(p =>
                            string.Equals(p, normSel, StringComparison.OrdinalIgnoreCase));
                        if (primIdx < 0)
                        {
                            primIdx = 0;
                        }

                        var primary = workOrdered[primIdx];
                        var sprite = primary.Sprite2DFoliageTarget;
                        EntityEmulatedPreviewRebakeContext? emulatedRebake = null;
                        var anchorOffset = Vector3.Zero;
                        var placementApplied = false;
                        var isParityCatalogEntity = EntityTextureParityCatalog.IsCatalogued(normSel);
                        if (isEmulatedEntityModel)
                        {
                            var prof = ResolvePreviewMeshNativeProfile(previewNativeProfile);
                            var idlePh = ComputeDeterministicIdlePhase(archivePath, prof.Name);
                            emulatedRebake = new EntityEmulatedPreviewRebakeContext
                            {
                                PackZipPath = inputZipPath,
                                AssetArchivePath = normSel,
                                NativeRootDirectory = prof.RootDirectory,
                                NativeProfileName = prof.Name,
                                NativeParsedVersion = prof.ParsedVersion?.ToString(),
                                ModelDefaultNamespace = modelDefaultNs,
                                IdlePhase01 = idlePh,
                                PreviewPoseId = EntityPreviewBuildContext.CurrentPoseId,
                                PreviewSizeId = EntityPreviewBuildContext.CurrentSizeId,
                                OrderedTextureZipPaths = orderedModelTextures.ToArray()
                            };
                            EntityPreviewPlacement.TryPopulateRebakeElementPartIds(
                                emulatedRebake,
                                prof,
                                mergedModel.Elements.Count);
                            EntityPreviewPlacement.TryMeasureMergedModelPartCentroidsY(
                                mergedModel,
                                emulatedRebake.ElementPartIds!,
                                out var bindBodyY,
                                out var bindHeadY,
                                out var bindLegY);
                            var placement = EntityPreviewPlacement.ApplyToPreviewVertices(
                                verts,
                                MinecraftModelBaker.FloatsPerVertex,
                                emulatedRebake.ElementPartIds!);
                            anchorOffset = placement.AnchorOffset;
                            placementApplied = true;
                            emulatedRebake.LastGroundContactY = placement.GroundContactY;
                            emulatedRebake.LastGroundLiftY = placement.GroundLiftY;
                            var placementYOffset = placement.AnchorOffset.Y + placement.GroundLiftY;
                            emulatedRebake.LastBodyCentroidY = bindBodyY != 0f ? bindBodyY + placementYOffset : placement.BodyCentroidY;
                            emulatedRebake.LastHeadCentroidY = bindHeadY != 0f ? bindHeadY + placementYOffset : placement.HeadCentroidY;
                            emulatedRebake.LastLegCentroidY = bindLegY != 0f ? bindLegY + placementYOffset : placement.LegCentroidY;
                            emulatedRebake.PackConverterCpuMeshFingerprint =
                                PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
                                    verts,
                                    MinecraftModelBaker.FloatsPerVertex);
                        }

                        var subject = new PreviewModelSubject
                        {
                            InterleavedVertices = verts,
                            Indices = indices,
                            DrawBatches = batchesList.ToArray(),
                            Materials = materials,
                            PrimaryMaterialIndex = primIdx,
                            Sprite2DFoliageTarget = sprite,
                            EnableRenderTimeAnimation = isEmulatedEntityModel && !isParityCatalogEntity,
                            AnimationPreset = isEmulatedEntityModel ? "entity_emulated" : null,
                            EmulatedRebake = emulatedRebake,
                            MeshProvenance = meshProvenance,
                            EntityPreviewAnchorOffset = anchorOffset,
                            EntityPreviewPlacementApplied = placementApplied
                        };

                        if (emulatedRebake is not null)
                        {
                            emulatedRebake.MeshProvenance = meshProvenance;
                        }

                        var png = PreviewComposer.ComposePreview(primary);
                        var dbg = options.BrickProbePreviewDebug ? primary.BrickProbeDebugText : null;
                        var previewSubject = PreviewPathPolicy.ShouldUseFlatItemPlane(normSel, sprite)
                            ? null
                            : subject;
                        return new PreviewDetailedResult(png, materials[primIdx], dbg, previewSubject, meshProvenance);
                    }
                }
            }

            var single = new List<TextureWorkItem> { target };

            await NormalHeightGenerator.GenerateAsync(single, options, null, cancellationToken).ConfigureAwait(false);
            await SpecularGenerator.GenerateAsync(single, options, null, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var legacyPng = PreviewComposer.ComposePreview(target);
            var legacyMaps = PreviewTextureMapsLoader.Load(target);
            var legacyDbg = options.BrickProbePreviewDebug ? target.BrickProbeDebugText : null;
            return new PreviewDetailedResult(legacyPng, legacyMaps, legacyDbg);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    private static MinecraftNativeProfile? ResolveNativeMinecraftDataProfile(string inputZipPath, string extractedPackDir)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        if (!Directory.Exists(root))
        {
            return null;
        }

        return MinecraftNativeProfileResolver.ResolveForPreview(root, inputZipPath, extractedPackDir);
    }

    /// <summary>
    /// Entity mesh / rebake must never use the placeholder <c>unknown</c> profile (blocks geometry IR under <c>geometry/unknown/</c>).
    /// </summary>
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

    private static bool IsEntityTextureArchivePath(string archivePath) =>
        archivePath.Replace('\\', '/').Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetAssetNamespace(string archivePath)
    {
        var parts = archivePath.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
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

    /// <summary>
    /// Stable animation phase for explore / initial bake so "Set Preview" does not jump keyframes.
    /// Render-tab playback uses wall-clock time via <see cref="EntityEmulatedPreviewRebaker"/>.
    /// </summary>
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

            // 0..120s window — enough for multi-clip breeze cycles without wrapping too fast.
            return ((h & 0x7fffffff) % 120_000) / 1000f;
        }
    }

}

