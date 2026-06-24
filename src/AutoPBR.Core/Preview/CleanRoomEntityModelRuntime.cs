using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Clean-room entity rigs sized to match vanilla <c>net.minecraft.client.model</c> cuboid dimensions (parts +
/// widths/heights/depths in tex/model space). Implementation stays independent of Mojang source; numbers are the spec.
/// Baby scale conventions split at Java game release 26.1 (see <see cref="UsesPostBabyModelUpdate"/>); 26.0.x tracks pre-26.1.
/// </summary>
/// <remarks>
/// <b>Batch parity backlog:</b> keep <see cref="TryBuildSpecific"/> branches ordered before broad substring keys (e.g. <c>bee_stinger</c> before <c>bee</c>,
/// <c>horse_zombie</c> before <c>zombie</c>, <c>dragon_fireball</c> before <c>dragon</c>). Each top-level branch has a stable 1-based slot for
/// <see cref="EntityGpuBoneDispatchKind.SpecificModelSlot"/> GPU bone cache; reordering or inserting branches shifts slots. Use <see cref="ClassifyEntityTextureRoute"/> against pinned
/// <c>client.jar</c> (<c>EntityTextureRoutingInventoryTests</c>) to keep vanilla <c>assets/minecraft/textures/entity/**/*.png</c> off family fallbacks and
/// out of <see cref="EntityRigFamily.Unknown"/>. Mojang artifacts under <c>tools/minecraft-parity/</c> support repeatable javap batches.
/// Vanilla Java <b>26.1.2</b> entity diffuse scope is additionally driven by <c>Data/minecraft-native/minecraft_26.1.2_entity_textures.json</c> plus
/// <c>minecraft_26.1.2_entity_texture_model_manifest.json</c> (<see cref="EntityTextureParityCatalog"/>): catalogued paths never use quadruped/fly/aquatic/humanoid family meshes.
/// Lifted vanilla <c>*Animation</c> IR under <c>docs/generated/animation/26.1.2/</c> is cross-walked to parity <c>builder_method</c> ids via
/// <c>minecraft_26.1.2_cleanroom_entity_animation_map.json</c> (<see cref="EntityCleanRoomAnimationMap"/>).
/// Generated <c>docs/generated/minecraft-client-model-index-*.json</c> (from <c>tools/Generate-MinecraftClientModelIndex.ps1</c>) lists official model classes + <c>javap -public</c>
/// output per game version. For <c>net.minecraft.client.animation.definitions.*Animation</c>, the same run also emits <c>javap -c</c> disassembly
/// (keyframes live in <c>&lt;clinit&gt;</c>) under <c>minecraft-client-model-index-&lt;ver&gt;-animation-init/</c>; JSON rows reference it via <c>javapBytecodeCRelPath</c>.
/// </remarks>
internal sealed partial class CleanRoomEntityModelRuntime : IEntityModelRuntime
{
    private static readonly string[] HumanoidKeys =
    [
        "zombie", "skeleton", "stray", "husk", "drowned", "player", "steve", "alex",
        "villager", "witch", "pillager", "illager", "vindicator", "evoker", "wandering_trader",
        "enderman", "piglin", "zombified_piglin"
    ];

    private static readonly string[] QuadrupedKeys =
    [
        "cow", "mooshroom", "pig", "sheep", "wolf", "fox", "ocelot", "cat", "horse",
        "donkey", "mule", "camel", "goat", "llama", "trader_llama", "panda", "polar_bear", "rabbit"
    ];

    /// <summary>
    /// Whether vanilla LER <c>scale(-1,-1,1)</c> should fold as <c>LocalToParent * S</c> (same multiply order as
    /// <see cref="ApplyEquineLivingEntityRendererPreviewBasis"/>) for mesh + fast GPU bones. Horse/donkey/mule use dedicated equine paths.
    /// </summary>
    internal static bool UsesQuadrupedLerMirrorRightComposeLocalChain(string stemLower, string normalizedAssetPath)
    {
        var stem = stemLower;
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (stem.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("mule", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // JVM stems like adultfelinemodel falsely match quadruped key "cat"; feline uses cow-class LER via JVM gate.
        if (stem.Contains("feline", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("rabbit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ContainsAny(stem, QuadrupedKeys))
        {
            return true;
        }

        // Adult/baby polar bears use stem "polarbear" / "polarbear_baby" under .../entity/bear/ — not a substring of "polar_bear".
        return norm.Contains("/textures/entity/bear/", StringComparison.OrdinalIgnoreCase) &&
            stem.Contains("polarbear", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] FlyingKeys =
    [
        "bat", "bee", "parrot", "phantom", "vex", "ghast", "blaze", "allay"
    ];

    private static readonly string[] AquaticKeys =
    [
        "salmon", "cod", "pufferfish", "tropical_fish", "squid", "glow_squid", "dolphin", "guardian", "turtle"
    ];

    private enum EntityRigFamily
    {
        Unknown,
        Humanoid,
        Quadruped,
        Flying,
        Aquatic
    }

    public bool TryBuildStaticMesh(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance,
        bool applyGeometryIrSetupAnimMotion = false,
        bool pairDoubleChestPreviewHalves = true)
    {
        mergedModel = null!;
        meshProvenance = default;
        var norm = entityTextureAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) || !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var texRef = ToTextureRef(norm);
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = LooksLikeBabyTexture(stem, norm);
        if (!TryDispatchEntityStaticMeshBuild(
                norm,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                routeCache: null,
                applyGeometryIrSetupAnimMotion,
                out _,
                out mergedModel,
                out meshProvenance))
        {
            return false;
        }

        if (pairDoubleChestPreviewHalves &&
            TryMergeDoubleChestPartnerHalf(
                norm,
                profile,
                idlePhase01,
                animationTimeSeconds,
                applyGeometryIrSetupAnimMotion,
                ref mergedModel))
        {
            var partnerSuffix = norm.Contains("_left", StringComparison.OrdinalIgnoreCase) ? "+right half" : "+left half";
            meshProvenance = meshProvenance with
            {
                Detail = string.IsNullOrWhiteSpace(meshProvenance.Detail)
                    ? $"paired double chest {partnerSuffix}"
                    : $"{meshProvenance.Detail} · paired double chest {partnerSuffix}",
            };
        }

        var parityRule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        if (EntityPreviewSizeCatalog.IsSlimeFamilyBuilderMethod(parityRule?.BuilderMethod))
        {
            var squish = SlimeFamilyPreviewScale.ComputePreviewSquish(animationTimeSeconds);
            var size = EntityPreviewSizeCatalog.ResolveEffectiveSize(
                EntityPreviewBuildContext.CurrentSizeId,
                parityRule?.BuilderMethod);
            mergedModel = SlimeFamilyPreviewScale.ApplyRendererScale(mergedModel, size, squish);
        }

        PreviewDepthLayerResolver.EnrichMergedModel(
            mergedModel,
            parityRule?.GeometryIrOfficialJvm ?? parityRule?.DeobfuscatedModelClass);
        return true;
    }

    /// <summary>
    /// Fills <paramref name="scratch"/> with per-element model-space bone matrices (including vanilla LER
    /// <c>scale(-1,-1,1)</c> folded as <c>worldRoot * LocalToParent</c>, or <c>LocalToParent * worldRoot</c> for quadrupeds —
    /// see <see cref="UsesQuadrupedLerMirrorRightComposeLocalChain"/>) without tessellating cuboid faces.
    /// </summary>
    /// <remarks>
    /// Horse/donkey/mule and chicken entity diffuse paths (see <see cref="EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract"/>)
    /// use a full <see cref="TryBuildStaticMesh"/> and copy <see cref="ModelElement.LocalToParent"/> instead of pose capture.
    /// </remarks>
    public bool TryFillBoneMatricesFast(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        List<Matrix4x4> scratch,
        out int boneCount,
        EntityEmulatedPreviewRebakeContext? routeCacheOwner = null,
        bool applyGeometryIrSetupAnimMotion = true)
    {
        boneCount = 0;
        scratch.Clear();
        var norm = entityTextureAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) || !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            if (routeCacheOwner is not null)
            {
                routeCacheOwner.GpuBoneDispatchRoute = null;
            }

            return false;
        }

        var texRef = ToTextureRef(norm);
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = LooksLikeBabyTexture(stem, norm);
        if (EntityTextureParityCatalog.IsCatalogued(norm) &&
            TryBuildStaticMesh(
                norm,
                profile,
                idlePhase01,
                animationTimeSeconds,
                out var irMerged,
                out var irProvenance,
                applyGeometryIrSetupAnimMotion) &&
            irProvenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson)
        {
            if (irMerged.Elements.Count == 0)
            {
                if (routeCacheOwner is not null)
                {
                    routeCacheOwner.GpuBoneDispatchRoute = null;
                }

                return false;
            }

            foreach (var el in irMerged.Elements)
            {
                scratch.Add(el.LocalToParent);
            }

            boneCount = scratch.Count;
            if (routeCacheOwner is not null)
            {
                var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
                routeCacheOwner.GpuBoneDispatchRoute = rule is null
                    ? null
                    : EntityGpuBoneDispatchRoute.ForParity(rule.BuilderMethod, irProvenance.Detail);
            }

            return true;
        }

        EntityGpuBoneDispatchRoute discoveredRoute;
        using (EntityRigPoseCapture.Use(scratch))
        {
            if (!TryDispatchEntityStaticMeshBuild(
                    norm,
                    stem,
                    texRef,
                    profile,
                    isBaby,
                    idlePhase01,
                    animationTimeSeconds,
                    routeCacheOwner?.GpuBoneDispatchRoute,
                    applyGeometryIrSetupAnimMotion,
                    out discoveredRoute,
                    out _,
                    out _))
            {
                if (routeCacheOwner is not null)
                {
                    routeCacheOwner.GpuBoneDispatchRoute = null;
                }

                return false;
            }

            if (routeCacheOwner is not null)
            {
                routeCacheOwner.GpuBoneDispatchRoute =
                    discoveredRoute.Kind != EntityGpuBoneDispatchKind.None ? discoveredRoute : null;
            }
        }

        // Parity-catalog IR static meshes already folded LER once (ResolveGeometryIrParityEmitPlan); do not double-apply.
        if (EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(norm, stem) &&
            discoveredRoute.Kind != EntityGpuBoneDispatchKind.ParityCatalog)
        {
            ApplyLivingEntityRendererBasisToBoneMatrices(
                scratch,
                ResolveGeometryIrLerBasis(discoveredRoute.GeometryIrOfficialJvm, stem, norm));
        }

        boneCount = scratch.Count;
        return boneCount > 0;
    }

    private static void ApplyStandardLivingEntityRendererBasisToBoneMatrices(List<Matrix4x4> bones)
    {
        for (var i = 0; i < bones.Count; i++)
        {
            bones[i] = ApplyLivingEntityRendererColumnRootScale(bones[i]);
        }
    }

    private static void ApplyQuadrupedLivingEntityRendererBasisToBoneMatrices(List<Matrix4x4> bones)
    {
        var worldRoot = Matrix4x4.CreateScale(-1f, -1f, 1f);
        for (var i = 0; i < bones.Count; i++)
        {
            bones[i] = Matrix4x4.Multiply(bones[i], worldRoot);
        }
    }

    private static void ApplyLivingEntityRendererBasisToBoneMatrices(
        List<Matrix4x4> bones,
        GeometryIrLerBasisKind basis)
    {
        switch (basis)
        {
            case GeometryIrLerBasisKind.Skip:
                return;
            case GeometryIrLerBasisKind.RightComposeLocalChain:
                ApplyQuadrupedLivingEntityRendererBasisToBoneMatrices(bones);
                return;
            default:
                ApplyStandardLivingEntityRendererBasisToBoneMatrices(bones);
                return;
        }
    }


}

