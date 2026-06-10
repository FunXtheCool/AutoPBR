using System.Text.Json;

// ReSharper disable CheckNamespace

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// <c>BreezeModel</c> merges <c>createBodyLayer</c> (32²), <c>createWindLayer</c> (128²), and <c>createEyesLayer</c> (32²).
    /// Manifest paths select which layers emit; wind tiers use 128² UV space (see legacy <see cref="BuildBreeze"/>).
    /// </summary>
    private static bool TryBuildParityCatalogBreezeMeshFromGeometryIr(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        EntityTextureParityRule parityRule,
        bool applyGeometryIrSetupAnimMotion,
        string officialJvm,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        out MergedJavaBlockModel merged,
        out string? geometryIrOfficialJvm)
    {
        merged = null!;
        geometryIrOfficialJvm = officialJvm;
        _ = stem;
        _ = isBaby;

        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var isEyesTexture = norm.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase);
        var isWindTexture = norm.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase);

        var wave = Wave(animationTimeSeconds, 0.8f);
        var lerPlan = ResolveGeometryIrParityEmitPlan(
            officialJvm,
            stem,
            norm,
            deferLivingEntityRendererUntilAfterMotionPasses: applyGeometryIrSetupAnimMotion);
        var emitOptions = ApplyLivingEntityRendererEmitPlan(
            GeometryIrParityEmitPresetRegistry.CreateBreezeEmitOptions(
                profile,
                officialJvm,
                atlasW,
                atlasH,
                isEyesTexture,
                isWindTexture,
                idlePhase01,
                wave,
                animationTimeSeconds),
            lerPlan);

        var b = new RigBuilder(atlasW, atlasH);
        if (!TryEmitGeometryIrBodyLayer(b, geometryRoot, emitOptions, out _))
        {
            return false;
        }

        var built = b.Build(texRef, BuildBreezeCompanionTextureRefs(norm, isEyesTexture, isWindTexture));
        if (built.Elements.Count == 0)
        {
            return false;
        }

        if (applyGeometryIrSetupAnimMotion)
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

        if (applyGeometryIrSetupAnimMotion)
        {
            TryApplyDefinitionAnimationGeometryIrPreviewPass(
                parityRule.BuilderMethod,
                normalizedAssetPath,
                profile,
                isBaby,
                animationTimeSeconds,
                built,
                geometryRoot,
                emitOptions,
                skipBreezeIdleWind: !isWindTexture && !isEyesTexture);
        }

        merged = FinishGeometryIrMeshLivingEntityRendererBasis(built, lerPlan);

        if (EntityRigPoseCapture.IsActive)
        {
            foreach (var el in merged.Elements)
            {
                EntityRigPoseCapture.Append(el.LocalToParent);
            }
        }

        return true;
    }

    private static Dictionary<string, string>? BuildBreezeCompanionTextureRefs(
        string normalizedAssetPath,
        bool isEyesTexture,
        bool isWindTexture)
    {
        if (isEyesTexture || isWindTexture)
        {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["eyes"] = CompanionDiffuseTextureRefFromSiblingFileStem(normalizedAssetPath, "breeze_eyes"),
            ["wind"] = CompanionDiffuseTextureRefFromSiblingFileStem(normalizedAssetPath, "breeze_wind"),
        };
    }
}
