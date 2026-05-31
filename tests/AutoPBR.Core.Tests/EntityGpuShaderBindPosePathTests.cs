using System.Numerics;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Simulates genesis.vert bind-pose entity branch (boneCount&gt;0, gpuSkinning=0, previewSpace=0).
/// </summary>
public sealed class EntityGpuShaderBindPosePathTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Gpu_bind_pose_shader_path_matches_cpu_rebake_for_cow()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mergedBind));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedBind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            mergedBind, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mergedBind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule!, path, stem, false, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm });

        EntityPreviewPlacement.ApplyToPreviewVertices(
            cpuVerts, MinecraftModelBaker.FloatsPerVertex, partIds);
        var gpuPlacement = EntityPreviewPlacement.ApplyToGpuBindVertices(gpuVerts, partIds);

        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        const int gpuStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        Assert.Equal(cpuVerts.Length / cpuStride, gpuVerts.Length / gpuStride);

        var maxErr = 0f;
        for (var vi = 0; vi < gpuVerts.Length / gpuStride; vi++)
        {
            var gi = vi * gpuStride;
            var ci = vi * cpuStride;
            var pBind = new Vector3(gpuVerts[gi], gpuVerts[gi + 1], gpuVerts[gi + 2]);
            var shaderPos = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(pBind);
            shaderPos.Y += gpuPlacement.GroundLiftY;
            var cpuPos = new Vector3(cpuVerts[ci], cpuVerts[ci + 1], cpuVerts[ci + 2]);
            maxErr = MathF.Max(maxErr, Vector3.Distance(shaderPos, cpuPos));
        }

        Assert.True(maxErr <= 0.02f, $"max vertex err={maxErr:F4}");
    }
}
