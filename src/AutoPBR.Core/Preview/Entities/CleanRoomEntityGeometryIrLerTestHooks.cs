using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>LER classification + test-only geometry IR emit helpers (META-001 Wave B).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{
    internal enum GeometryIrLerBasisKind
    {
        Skip,
        StandardWorldRoot,
        RightComposeLocalChain,
        EquineDedicated
    }

    /// <summary>
    /// Flat <c>PartPose.offset</c> root-sibling quadruped factories need cow-class LER fold
    /// (<c>LocalToParent * S</c>), not default <c>S * LocalToParent</c>.
    /// </summary>
    internal static bool UsesFlatPartPoseOffsetQuadrupedJvm(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".QuadrupedModel", StringComparison.Ordinal) ||
               officialJvmName.Contains(".monster.creeper.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.armadillo.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.turtle.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.cow.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.pig.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.sheep.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.wolf.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.goat.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.fox.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.feline.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.panda.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.polarbear.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hosts that need default <c>S * LocalToParent</c> (hoglin / ravager / rabbit class).</summary>
    internal static bool UsesComposedOffsetAndRotationBodyDefaultLerJvm(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".monster.hoglin.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".monster.ravager.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.rabbit.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves LER <c>scale(-1,-1,1)</c> multiply order for geometry IR parity emit (catalog + tests).</summary>
    internal static bool ResolveGeometryIrLerMirrorRightComposeLocalChain(
        string? officialJvmName,
        string? stemLower,
        string? normalizedAssetPath)
        => ResolveGeometryIrLerBasis(officialJvmName, stemLower, normalizedAssetPath) ==
           GeometryIrLerBasisKind.RightComposeLocalChain;

    internal static GeometryIrLerBasisKind ResolveGeometryIrLerBasis(
        string? officialJvmName,
        string? stemLower,
        string? normalizedAssetPath)
    {
        var stem = (stemLower ?? "").ToLowerInvariant();
        var norm = normalizedAssetPath ?? "";
        if (EntityGpuBoneFillPolicy.SkipsLivingEntityRendererBasis(stem))
        {
            return GeometryIrLerBasisKind.Skip;
        }

        if (UsesEquineGeometryIrPreviewBasis(officialJvmName, stem, norm))
        {
            return GeometryIrLerBasisKind.EquineDedicated;
        }

        if (UsesComposedOffsetAndRotationBodyDefaultLerJvm(officialJvmName))
        {
            return GeometryIrLerBasisKind.StandardWorldRoot;
        }

        return UsesFlatPartPoseOffsetQuadrupedJvm(officialJvmName) ||
               UsesQuadrupedLerMirrorRightComposeLocalChain(stem, norm)
            ? GeometryIrLerBasisKind.RightComposeLocalChain
            : GeometryIrLerBasisKind.StandardWorldRoot;
    }

    /// <summary>Geometry IR parity emit for horse/donkey/mule JVM rows (no entity texture path in tests).</summary>
    internal static bool UsesEquineGeometryIrPreviewBasis(
        string? officialJvmName,
        string stemLower,
        string? normalizedAssetPath)
    {
        var norm = normalizedAssetPath ?? "";
        if (norm.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("mule", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(officialJvmName) &&
            officialJvmName.Contains(".animal.equine.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stemLower.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
               stemLower.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
               stemLower.Contains("mule", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>LER preview basis for parity-catalog and test geometry IR emit.</summary>
    internal static MergedJavaBlockModel ApplyGeometryIrParityLivingEntityRendererPreviewBasis(
        string officialJvmName,
        JsonElement? geometryRootOverride,
        string? stem,
        string? normalizedAssetPath,
        MergedJavaBlockModel built)
    {
        var resolvedStem = stem;
        if (string.IsNullOrWhiteSpace(resolvedStem))
        {
            var dot = officialJvmName.LastIndexOf('.');
            resolvedStem = dot >= 0 ? officialJvmName[(dot + 1)..] : officialJvmName;
        }

        resolvedStem = resolvedStem.ToLowerInvariant();
        return ApplyLivingEntityRendererPreviewBasis(
            built,
            ResolveGeometryIrLerBasis(officialJvmName, resolvedStem, normalizedAssetPath));
    }

    /// <summary>Hand-built parity-catalog mesh for bind-pose comparison tests (no geometry IR).</summary>
    internal static bool TryBuildLegacyParityCatalogMeshForTests(
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        EntityTextureParityRule rule,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel mesh)
    {
        mesh = null!;
        if (!string.Equals(rule.BuilderMethod, "Breeze", StringComparison.Ordinal))
        {
            return false;
        }

        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var texRef = ToTextureRef(norm);
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = LooksLikeBabyTexture(stem, norm);
        var wave = Wave(animationTimeSeconds, 0.8f);
        var shootHeadPitchRad = 0f;
        if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(profile, animationTimeSeconds, out var shootHeadDeg))
        {
            shootHeadPitchRad = shootHeadDeg.X * (MathF.PI / 180f);
        }

        var shootHeadPos = Vector3.Zero;
        if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadPosition(profile, animationTimeSeconds, out var shootHeadTranslation))
        {
            shootHeadPos = shootHeadTranslation;
        }

        mesh = BuildBreeze(
            norm,
            texRef,
            profile,
            isBaby,
            swirl: idlePhase01 * 0.6f + wave * 0.2f,
            windAnimTimeSeconds: animationTimeSeconds,
            shootHeadAdditivePitchRad: shootHeadPitchRad,
            shootHeadAdditiveTranslate: shootHeadPos);
        return true;
    }
}
