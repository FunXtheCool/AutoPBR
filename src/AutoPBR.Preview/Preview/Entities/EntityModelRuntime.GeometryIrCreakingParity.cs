using System.Text.Json;

// ReSharper disable CheckNamespace

namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{
    /// <summary>
    /// <c>CreakingModel</c> merges <c>createBodyLayer</c> and <c>createEyesLayer</c> (head-only retain on the same mesh).
    /// Main <c>creaking.png</c> preview emits body + head overlay on sibling <c>creaking_eyes.png</c>.
    /// </summary>
    private static bool TryBuildParityCatalogCreakingMeshFromGeometryIr(
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
        var isEyesTexture = norm.Contains("creaking_eyes", StringComparison.OrdinalIgnoreCase);

        var wave = Wave(animationTimeSeconds, 0.8f);
        var lerPlan = ResolveGeometryIrParityEmitPlan(
            officialJvm,
            stem,
            norm,
            deferLivingEntityRendererUntilAfterMotionPasses: applyGeometryIrSetupAnimMotion);
        var emitOptions = ApplyLivingEntityRendererEmitPlan(
            GeometryIrParityEmitPresetRegistry.CreateCreakingEmitOptions(
                profile,
                officialJvm,
                atlasW,
                atlasH,
                isEyesTexture,
                idlePhase01,
                wave,
                animationTimeSeconds)
                .WithOfficialJvmPoseComposeDefaults(officialJvm)
                with
                {
                    OfficialJvmName = officialJvm,
                    NormalizedAssetPath = norm,
                },
            lerPlan);

        var b = new RigBuilder(atlasW, atlasH);
        if (!TryEmitGeometryIrBodyLayer(b, geometryRoot, emitOptions, out _))
        {
            return false;
        }

        if (!isEyesTexture)
        {
            var eyesOverlayOptions = emitOptions with
            {
                ShouldEmitPartCuboids = static partId =>
                    string.Equals(partId, "head", StringComparison.OrdinalIgnoreCase),
                ShouldEmitIrCuboid = static cuboid =>
                    !GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var faceMask) || faceMask.Length == 0,
                ResolvePartTextureKey = static partId =>
                    string.Equals(partId, "head", StringComparison.OrdinalIgnoreCase) ? "#eyes" : null,
            };
            if (!TryEmitGeometryIrBodyLayer(b, geometryRoot, eyesOverlayOptions, out _))
            {
                return false;
            }
        }

        var built = b.Build(texRef, BuildCreakingCompanionTextureRefs(norm, isEyesTexture));
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
                emitOptions,
                norm);
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
                emitOptions);
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

    private static Dictionary<string, string>? BuildCreakingCompanionTextureRefs(
        string normalizedAssetPath,
        bool isEyesTexture)
    {
        if (isEyesTexture)
        {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["eyes"] = CompanionDiffuseTextureRefFromSiblingFileStem(normalizedAssetPath, "creaking_eyes"),
        };
    }
}
