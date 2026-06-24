// ReSharper disable CheckNamespace
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
    /// Where to fold vanilla LER for one geometry IR mesh build (emit root vs single post-batch).
    /// </summary>
    internal readonly struct GeometryIrLivingEntityRendererEmitPlan
    {
        public Matrix4x4 EmitRootTransform { get; init; }

        public bool ApplyPostLivingEntityRendererBasis { get; init; }

        public GeometryIrLerBasisKind Basis { get; init; }
    }

    /// <summary>
    /// Resolves LER folding for parity/catalog emit. Motion passes (setupAnim, definition anim) stay in model space;
    /// LER is applied <b>once</b> after them when <paramref name="deferLivingEntityRendererUntilAfterMotionPasses"/> is true.
    /// Static bind folds LER once after emit via
    /// <see cref="ApplyLivingEntityRendererColumnRootScale(MergedJavaBlockModel)"/> (column
    /// <c>S * LocalToParent</c> on points). Motion passes stay model-space until the single post-batch when deferred.
    /// </summary>
    internal static GeometryIrLivingEntityRendererEmitPlan ResolveGeometryIrParityEmitPlan(
        string? officialJvmName,
        string? stemLower,
        string? normalizedAssetPath,
        bool deferLivingEntityRendererUntilAfterMotionPasses)
    {
        var basis = ResolveGeometryIrLerBasis(officialJvmName, stemLower, normalizedAssetPath);
        if (basis is GeometryIrLerBasisKind.Skip)
        {
            return new GeometryIrLivingEntityRendererEmitPlan
            {
                EmitRootTransform = Matrix4x4.Identity,
                ApplyPostLivingEntityRendererBasis = false,
                Basis = basis,
            };
        }

        return new GeometryIrLivingEntityRendererEmitPlan
        {
            EmitRootTransform = Matrix4x4.Identity,
            ApplyPostLivingEntityRendererBasis = true,
            Basis = basis,
        };
    }

    internal static GeometryIrMeshEmitOptions ApplyLivingEntityRendererEmitPlan(
        GeometryIrMeshEmitOptions options,
        in GeometryIrLivingEntityRendererEmitPlan plan)
    {
        var root = plan.EmitRootTransform;
        if (options.RootTransform != Matrix4x4.Identity)
        {
            root = root == Matrix4x4.Identity
                ? options.RootTransform
                : EntityParityTemplate.Mul(options.RootTransform, root);
        }

        return options with { RootTransform = root };
    }

    internal static MergedJavaBlockModel FinishGeometryIrMeshLivingEntityRendererBasis(
        MergedJavaBlockModel mesh,
        in GeometryIrLivingEntityRendererEmitPlan plan) =>
        plan.ApplyPostLivingEntityRendererBasis
            ? ApplyLivingEntityRendererPreviewBasis(mesh, plan.Basis)
            : mesh;

    /// <summary>
    /// Flat <c>PartPose.offset</c> root-sibling quadruped factories (cow, pig, panda, creeper class).
    /// Production emit uses <see cref="GeometryIrLerBasisKind.StandardWorldRoot"/> →
    /// <see cref="ApplyLivingEntityRendererColumnRootScale(MergedJavaBlockModel)"/> after model-space walk (column <c>S * (T * R)</c> on points).
    /// Legacy per-element <c>M * S</c> (<see cref="GeometryIrLerBasisKind.RightComposeLocalChain"/>) detaches rotated body cuboids.
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

    internal static bool IsPolarBearGeometryIrJvm(string? officialJvmName) =>
        string.Equals(
            officialJvmName,
            "net.minecraft.client.model.animal.polarbear.PolarBearModel",
            StringComparison.Ordinal);

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
        switch (EntityPreviewDebugSettings.LerBasisOverride)
        {
            case EntityPreviewLerBasisOverride.StandardWorldRoot:
                return GeometryIrLerBasisKind.StandardWorldRoot;
            case EntityPreviewLerBasisOverride.RightComposeLocalChain:
                return GeometryIrLerBasisKind.RightComposeLocalChain;
            case EntityPreviewLerBasisOverride.Skip:
                return GeometryIrLerBasisKind.Skip;
        }

        var stem = (stemLower ?? "").ToLowerInvariant();
        if (EntityGpuBoneFillPolicy.SkipsLivingEntityRendererBasis(stem))
        {
            return GeometryIrLerBasisKind.Skip;
        }

        if (!string.IsNullOrWhiteSpace(normalizedAssetPath))
        {
            var norm = normalizedAssetPath.Replace('\\', '/');
            if (norm.Contains("/textures/entity/boat/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/minecart/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/bed/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/signs/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/banner/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/banner_base", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/bell/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/decorated_pot/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/conduit/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/beacon/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/end_portal/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/experience/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/fishing/", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/guardian/guardian_beam", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("/textures/entity/enderdragon/dragon_fireball", StringComparison.OrdinalIgnoreCase))
            {
                return GeometryIrLerBasisKind.Skip;
            }
        }

        if (UsesLivingEntityRendererDespiteObjectPackage(officialJvmName, normalizedAssetPath))
        {
            return GeometryIrLerBasisKind.StandardWorldRoot;
        }

        if (!string.IsNullOrWhiteSpace(officialJvmName) &&
            officialJvmName.Contains(".object.", StringComparison.Ordinal))
        {
            return GeometryIrLerBasisKind.Skip;
        }

        if (UsesEquineGeometryIrPreviewBasis(officialJvmName, stem, normalizedAssetPath))
        {
            return GeometryIrLerBasisKind.EquineDedicated;
        }

        // Ghast / happy ghast: IR reorients tentacle +Y cuboids to −Y hang for preview without LER.
        // Column-root LER would flip that hang back upward through the body shell (see vanilla reference).
        if (GeometryIrEmitPolicy.IsGhastFamilyJvm(officialJvmName) ||
            GeometryIrEmitPolicy.IsGhastFamilyTexturePath(normalizedAssetPath))
        {
            return GeometryIrLerBasisKind.Skip;
        }

        return GeometryIrLerBasisKind.StandardWorldRoot;
    }

    /// <summary>
    /// Models under <c>net.minecraft.client.model.object.*</c> that still render through vanilla
    /// <c>LivingEntityRenderer</c> (column <c>scale(-1,-1,1)</c> before the model tree).
    /// </summary>
    internal static bool UsesLivingEntityRendererDespiteObjectPackage(
        string? officialJvmName,
        string? normalizedAssetPath = null)
    {
        if (!string.IsNullOrWhiteSpace(officialJvmName) &&
            officialJvmName.Contains(".object.armorstand.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedAssetPath))
        {
            return normalizedAssetPath.Replace('\\', '/')
                .Contains("/textures/entity/armorstand/", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>Vanilla <c>LivingEntityRenderer</c> mirror applied once before the model tree.</summary>
    internal static Matrix4x4 LivingEntityRendererPreviewRootScale { get; } =
        Matrix4x4.CreateScale(-1f, -1f, 1f);

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

    /// <summary>Parity emit without LER post-basis (model-space <c>LocalToParent</c> for reference_java compare).</summary>
    internal static MergedJavaBlockModel? TryBuildGeometryIrModelSpaceParityMeshForTests(
        string texRef,
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        JsonElement geometryRootOverride,
        out string? failureReason)
    {
        var b = new RigBuilder(atlasWidth, atlasHeight);
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            Fidelity = GeometryIrEmitFidelity.Parity,
            PreviewDegenerateAxisThickness = 0f,
            OfficialJvmName = officialJvmName,
        };
        if (!TryEmitGeometryIrBodyLayer(b, geometryRootOverride, options, out failureReason))
        {
            return null;
        }

        return b.Build(texRef);
    }

    /// <summary>Hand-built CleanRoom catalog mesh for bind-pose comparison (bypasses geometry IR resolver).</summary>
    internal static bool TryBuildCleanRoomParityCatalogMeshForTests(
        string builderMethod,
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        out MergedJavaBlockModel mesh)
    {
        mesh = null!;
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var texRef = ToTextureRef(norm);
        var isBaby = LooksLikeBabyTexture(stem, norm);
        return TryInvokeParityCatalogBuilder(
            builderMethod,
            norm,
            stem,
            texRef,
            profile,
            isBaby,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out mesh);
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
