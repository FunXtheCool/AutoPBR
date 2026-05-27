using System.Text.Json;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// Emits bytecode-lifted geometry IR (<c>extractionStatus: ok</c>) for parity-catalog textures.
    /// Legacy hand-written <see cref="TryInvokeParityCatalogBuilder"/> rigs are used only when no
    /// <c>ok</c> shard exists, or when IR emit fails and no lifted tree is available to trust.
    /// </summary>
    private static bool TryBuildParityCatalogMeshFromGeometryIr(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        EntityTextureParityRule parityRule,
        bool applyGeometryIrSetupAnimMotion,
        out MergedJavaBlockModel merged,
        out string? geometryIrOfficialJvm)
    {
        merged = null!;
        geometryIrOfficialJvm = null;
        var tier = GeometryIrParityPolicy.GetTier(parityRule.BuilderMethod);

        var canTryIr = !string.IsNullOrWhiteSpace(parityRule.GeometryIrOfficialJvm) ||
                       !string.IsNullOrWhiteSpace(parityRule.GeometryIrOfficialJvmBaby) ||
                       GeometryIrParityHandLiftJvmMap.TryGetHandLiftJvm(
                           parityRule.BuilderMethod,
                           normalizedAssetPath,
                           out _) ||
                       GeometryIrParityEquipmentJvmMap.TryResolveOfficialJvm(
                           parityRule.BuilderMethod,
                           normalizedAssetPath,
                           isBaby,
                           out _,
                           out _) ||
                       !string.IsNullOrWhiteSpace(parityRule.DeobfuscatedModelClassPreRestructure) ||
                       (!string.IsNullOrWhiteSpace(parityRule.DeobfuscatedModelClass) &&
                        !parityRule.DeobfuscatedModelClass.Contains("renderer", StringComparison.OrdinalIgnoreCase));
        if (!canTryIr)
        {
            return false;
        }

        if (!TryResolveParityCatalogGeometryIrRoot(
                parityRule,
                isBaby,
                profile,
                normalizedAssetPath,
                stem,
                out var resolvedJvm,
                out var geometryRoot))
        {
            return false;
        }

        geometryIrOfficialJvm = resolvedJvm;

        if (GeometryIrLiftPolicy.EvaluateDocument(geometryRoot) == GeometryIrLiftPolicyDecision.RejectForParity)
        {
            return false;
        }

        if (string.IsNullOrEmpty(geometryIrOfficialJvm))
        {
            geometryIrOfficialJvm = parityRule.GeometryIrOfficialJvm ?? parityRule.DeobfuscatedModelClass ?? "";
        }

        var officialJvm = geometryIrOfficialJvm;
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, geometryRoot);

        if (!TryResolveParityCatalogGeometryIrAtlasDimensions(
                geometryRoot,
                parityRule,
                normalizedAssetPath,
                out var atlasW,
                out var atlasH))
        {
            return false;
        }

        if (string.Equals(parityRule.BuilderMethod, "Breeze", StringComparison.Ordinal))
        {
            return TryBuildParityCatalogBreezeMeshFromGeometryIr(
                normalizedAssetPath,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                parityRule,
                applyGeometryIrSetupAnimMotion,
                officialJvm,
                geometryRoot,
                atlasW,
                atlasH,
                out merged,
                out geometryIrOfficialJvm);
        }

        var wave = Wave(animationTimeSeconds, 0.8f);
        var emitOptions = GeometryIrParityEmitPresetRegistry.CreateEmitOptions(
            parityRule.BuilderMethod,
            profile,
            isBaby,
            officialJvm,
            atlasW,
            atlasH,
            idlePhase01,
            wave);

        var b = new RigBuilder(atlasW, atlasH);
        if (!TryEmitGeometryIrBodyLayer(b, geometryRoot, emitOptions, out _))
        {
            return false;
        }

        var built = b.Build(texRef);
        if (built.Elements.Count == 0)
        {
            return false;
        }

        if (applyGeometryIrSetupAnimMotion)
        {
            if (tier == GeometryIrParityTier.IrGeometryPreviewAnim &&
                string.Equals(parityRule.BuilderMethod, "Chicken", StringComparison.Ordinal))
            {
                ComputeChickenParityPreviewDrivers(
                    animationTimeSeconds,
                    idlePhase01,
                    wave,
                    out var headPitchRad,
                    out var headYawRad,
                    out var wingZ,
                    out var rLeg,
                    out var lLeg);
                ApplyChickenGeometryIrPreviewAnimPass(
                    built,
                    geometryRoot,
                    isBaby,
                    headPitchRad,
                    headYawRad,
                    wingZ,
                    rLeg,
                    lLeg,
                    emitOptions);
            }
            else
            {
                _ = TryApplySetupAnimGeometryIrPreviewPass(
                    parityRule,
                    officialJvm,
                    built,
                    geometryRoot,
                    isBaby,
                    animationTimeSeconds,
                    idlePhase01,
                    wave,
                    emitOptions);
            }
        }

        merged = ApplyParityCatalogGeometryIrPreviewBasis(
            parityRule.BuilderMethod,
            officialJvm,
            normalizedAssetPath,
            stem,
            texRef,
            built);

        TryApplyDefinitionAnimationGeometryIrPreviewPass(
            parityRule.BuilderMethod,
            normalizedAssetPath,
            profile,
            isBaby,
            animationTimeSeconds,
            merged,
            geometryRoot,
            emitOptions);

        if (EntityRigPoseCapture.IsActive)
        {
            foreach (var el in merged.Elements)
            {
                EntityRigPoseCapture.Append(el.LocalToParent);
            }
        }

        return true;
    }

    /// <summary>
    /// When an <c>ok</c> lifted shard exists, do not fall back to guessed hand-built cuboids — they can
    /// diverge from bytecode and hide lift regressions.
    /// </summary>
    /// <summary>
    /// GPU-bone and stem-specific axolotl paths prefer catalog geometry IR when the texture is catalogued.
    /// </summary>
    private static bool TryBuildAxolotlMeshPreferGeometryIr(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel merged)
    {
        merged = null!;
        if (!EntityTextureParityCatalog.IsCatalogued(normalizedAssetPath) ||
            EntityTextureParityCatalog.ResolveRule(normalizedAssetPath, stem) is not { } rule)
        {
            return false;
        }

        return TryBuildParityCatalogMeshFromGeometryIr(
            normalizedAssetPath,
            stem,
            texRef,
            profile,
            isBaby,
            idlePhase01,
            animationTimeSeconds,
            rule,
            applyGeometryIrSetupAnimMotion: true,
            out merged,
            out _);
    }

    internal static bool ShouldSuppressHandBuiltParityFallback(
        MinecraftNativeProfile profile,
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        string stem,
        bool isBaby,
        out string irFailureReason)
    {
        irFailureReason = "";
        if (!GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                profile, rule, normalizedAssetPath, stem, isBaby, out _, out _))
        {
            return false;
        }

        irFailureReason = ClassifyParityCatalogGeometryIrFailure(
            profile, rule, normalizedAssetPath, stem, isBaby);
        return true;
    }

    private static bool TryResolveParityCatalogGeometryIrRoot(
        EntityTextureParityRule rule,
        bool isBaby,
        MinecraftNativeProfile profile,
        string normalizedAssetPath,
        string stem,
        out string officialJvmName,
        out JsonElement geometryRoot) =>
        GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            profile,
            rule,
            normalizedAssetPath,
            stem,
            isBaby,
            out officialJvmName,
            out geometryRoot);

    /// <summary>
    /// Atlas size from manifest, lifted shard fields, materialized PNG, or parity-catalog builder defaults.
    /// </summary>
    private static bool TryResolveParityCatalogGeometryIrAtlasDimensions(
        JsonElement geometryRoot,
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        out int atlasW,
        out int atlasH)
    {
        atlasW = 0;
        atlasH = 0;
        if (rule.GeometryIrTextureWidth is > 0 and var rw && rule.GeometryIrTextureHeight is > 0 and var rh)
        {
            atlasW = rw;
            atlasH = rh;
            return true;
        }

        if (geometryRoot.TryGetProperty("textureWidth", out var tw) &&
            tw.TryGetInt32(out var tww) &&
            tww > 0 &&
            geometryRoot.TryGetProperty("textureHeight", out var th) &&
            th.TryGetInt32(out var thh) &&
            thh > 0)
        {
            atlasW = tww;
            atlasH = thh;
            return true;
        }

        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var rel = norm.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? norm["assets/".Length..] : norm;
        var pngPath = Path.Combine(AppContext.BaseDirectory, rel.Replace('/', Path.DirectorySeparatorChar));
        if (EntityTexturePngDimensions.TryRead(pngPath, out var pw, out var ph))
        {
            atlasW = pw;
            atlasH = ph;
            return true;
        }

        return GeometryIrParityAtlasDefaults.TryGetForBuilderMethod(rule.BuilderMethod, out atlasW, out atlasH);
    }

    /// <summary>
    /// Post–26.1 <c>Baby*Model</c> IR hosts are already baby-sized; only scale adult mesh hosts (e.g. <c>SkeletonModel</c>).
    /// </summary>
    private static BabyProfile ParityCatalogDefaultBabyProfile(
        MinecraftNativeProfile profile,
        bool isBaby,
        string? resolvedOfficialJvm) =>
        !isBaby
            ? BabyProfile.Adult
            : UsesPostBabyModelUpdate(profile) &&
              !string.IsNullOrWhiteSpace(resolvedOfficialJvm) &&
              GeometryIrParityJvmResolver.SimpleClassNameContainsBaby(resolvedOfficialJvm)
                ? BabyProfile.Adult
                : BabyProfile.VanillaUniformBaby;

    private static float ResolveDefaultPartScale(string partId, BabyProfile p)
    {
        var s = partId.ToLowerInvariant();
        if (s.Contains("head") || s.Contains("hat"))
        {
            return p.HeadScale;
        }

        if (s.Contains("leg") || s.Contains("arm") || s.Contains("fin") || s.Contains("tentacle") ||
            (s.Contains("wing") && !s.Contains("body")))
        {
            return p.LegScale;
        }

        return p.BodyScale;
    }

    private static MergedJavaBlockModel ApplyParityCatalogGeometryIrPreviewBasis(
        string builderMethod,
        string officialJvm,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MergedJavaBlockModel built)
    {
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var basis = ResolveGeometryIrLerBasis(officialJvm, stem, norm);
        if (basis == GeometryIrLerBasisKind.EquineDedicated)
        {
            var scale = string.Equals(builderMethod, "DonkeyMuleHorse", StringComparison.Ordinal)
                ? (texRef.Contains("/mule", StringComparison.OrdinalIgnoreCase) ? 0.92f : 0.87f)
                : 1f;
            return ApplyEquineLivingEntityRendererPreviewBasis(built, scale);
        }

        if (basis == GeometryIrLerBasisKind.Skip ||
            norm.Contains("/textures/entity/boat/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase))
        {
            return built;
        }

        return ApplyLivingEntityRendererPreviewBasis(built, basis);
    }

    /// <summary>
    /// Mirrors parity-catalog geometry IR build steps for survey diagnostics (no mesh output).
    /// </summary>
    internal static string ClassifyParityCatalogGeometryIrFailure(
        MinecraftNativeProfile profile,
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        string stem,
        bool isBaby)
    {
        if (!TryResolveParityCatalogGeometryIrRoot(
                rule,
                isBaby,
                profile,
                normalizedAssetPath,
                stem,
                out var surveyJvm,
                out var geometryRoot))
        {
            return "no_ok_shard";
        }

        if (GeometryIrLiftPolicy.EvaluateDocument(geometryRoot) == GeometryIrLiftPolicyDecision.RejectForParity)
        {
            return "lift_policy_reject";
        }

        if (!TryResolveParityCatalogGeometryIrAtlasDimensions(
                geometryRoot,
                rule,
                normalizedAssetPath,
                out var atlasW,
                out var atlasH))
        {
            return "atlas_failed";
        }

        var emitOptions = string.Equals(rule.BuilderMethod, "Breeze", StringComparison.Ordinal)
            ? GeometryIrParityEmitPresetRegistry.CreateBreezeEmitOptions(
                profile,
                surveyJvm,
                atlasW,
                atlasH,
                isEyesTexturePath: normalizedAssetPath.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase),
                isWindTexturePath: normalizedAssetPath.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase),
                idlePhase01: 0f,
                wave: 0f,
                animationTimeSeconds: 0f)
            : GeometryIrParityEmitPresetRegistry.CreateEmitOptions(
                rule.BuilderMethod,
                profile,
                isBaby,
                surveyJvm,
                atlasW,
                atlasH,
                idlePhase01: 0f,
                wave: 0f);

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);
        var b = new RigBuilder(atlasW, atlasH);
        if (!TryEmitGeometryIrBodyLayer(b, geometryRoot, emitOptions, out var emitReason))
        {
            return $"emit_failed:{emitReason ?? "unknown"}";
        }

        var elementCount = b.Build("survey").Elements.Count;
        if (elementCount == 0)
        {
            return "empty_elements";
        }

        if (partIds.Count != elementCount)
        {
            return "setup_anim_part_order_mismatch";
        }

        return "runtime_still_cleanroom";
    }
}
