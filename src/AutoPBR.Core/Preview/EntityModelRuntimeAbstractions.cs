using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Clean-room, code-driven entity model provider abstraction (no Mojang source reuse).
/// Separate implementations can target behavior differences between native profiles (e.g. 1.21.11 vs 26.1.2 baby rules).
/// </summary>
internal interface IEntityModelRuntime
{
    bool TryBuildStaticMesh(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel mergedModel,
        out PreviewMeshProvenance meshProvenance,
        bool applyGeometryIrSetupAnimMotion = false,
        bool pairDoubleChestPreviewHalves = true);

    /// <summary>
    /// Fast path for GPU bone uniforms: pose matrices without cuboid face allocation (see runtime implementation for fallbacks).
    /// </summary>
    bool TryFillBoneMatricesFast(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        List<Matrix4x4> scratch,
        out int boneCount,
        EntityEmulatedPreviewRebakeContext? routeCacheOwner = null,
        bool applyGeometryIrSetupAnimMotion = true);
}

internal static class EntityModelRuntimeFactory
{
    public static IEntityModelRuntime Create() => new CleanRoomEntityModelRuntime();
}

internal static class EntityModelRuntimeExtensions
{
    public static bool TryBuildStaticMesh(
        this IEntityModelRuntime runtime,
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel mergedModel,
        bool applyGeometryIrSetupAnimMotion = false,
        bool pairDoubleChestPreviewHalves = true) =>
        runtime.TryBuildStaticMesh(
            entityTextureAssetPath,
            profile,
            idlePhase01,
            animationTimeSeconds,
            out mergedModel,
            out _,
            applyGeometryIrSetupAnimMotion,
            pairDoubleChestPreviewHalves);
}

