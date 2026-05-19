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
        bool applyGeometryIrSetupAnimMotion = false)
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
        return TryDispatchEntityStaticMeshBuild(
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
            out meshProvenance);
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
        EntityEmulatedPreviewRebakeContext? routeCacheOwner = null)
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
                    applyGeometryIrSetupAnimMotion: true,
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

        // Geometry IR parity capture records post-basis LocalToParent (see TryBuildParityCatalogMeshFromGeometryIr).
        if (EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(norm, stem) &&
            discoveredRoute.Kind != EntityGpuBoneDispatchKind.ParityCatalog)
        {
            if (UsesQuadrupedLerMirrorRightComposeLocalChain(stem, norm))
            {
                ApplyQuadrupedLivingEntityRendererBasisToBoneMatrices(scratch);
            }
            else
            {
                ApplyStandardLivingEntityRendererBasisToBoneMatrices(scratch);
            }
        }

        boneCount = scratch.Count;
        return boneCount > 0;
    }

    private static void ApplyStandardLivingEntityRendererBasisToBoneMatrices(List<Matrix4x4> bones)
    {
        var worldRoot = Matrix4x4.CreateScale(-1f, -1f, 1f);
        for (var i = 0; i < bones.Count; i++)
        {
            bones[i] = Matrix4x4.Multiply(worldRoot, bones[i]);
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


}

