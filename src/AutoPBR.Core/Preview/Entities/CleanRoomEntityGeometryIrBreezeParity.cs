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
        var emitOptions = GeometryIrParityEmitPresetRegistry.CreateBreezeEmitOptions(
            profile,
            officialJvm,
            atlasW,
            atlasH,
            isEyesTexture,
            isWindTexture,
            idlePhase01,
            wave,
            animationTimeSeconds);

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

        merged = ApplyParityCatalogGeometryIrPreviewBasis(
            parityRule.BuilderMethod,
            officialJvm,
            normalizedAssetPath,
            stem,
            texRef,
            built);

        if (EntityRigPoseCapture.IsActive)
        {
            foreach (var el in merged.Elements)
            {
                EntityRigPoseCapture.Append(el.LocalToParent);
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, string>? BuildBreezeCompanionTextureRefs(
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
