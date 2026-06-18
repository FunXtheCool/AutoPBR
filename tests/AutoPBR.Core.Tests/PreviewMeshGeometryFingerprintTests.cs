using System.Numerics;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class PreviewMeshGeometryFingerprintTests
{
    [Fact]
    public void Fingerprint_changes_when_vertex_positions_change()
    {
        var a = new float[] { 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f };
        var fa = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(a, 12);
        var fb = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(b, 12);
        Assert.NotEqual(fa, fb);
    }

    [Fact]
    public void Fingerprint_changes_when_only_uvs_change()
    {
        var a = new float[] { 0f, 0f, 0f, 0f, 1f, 0f, 0.25f, 0.5f, 1f, 0f, 0f, 0f };
        var b = new float[] { 0f, 0f, 0f, 0f, 1f, 0f, 0.5f, 0.25f, 1f, 0f, 0f, 0f };
        var fa = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(a, 12);
        var fb = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(b, 12);
        Assert.NotEqual(fa, fb);
    }

    [Fact]
    public void Fingerprint_is_stable_for_identical_cpu_mesh()
    {
        const string path = "assets/minecraft/textures/entity/dolphin/dolphin.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, 0.3f, 0f, out var bind, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(bind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(bind, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));

        var fp1 = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(cpuVerts, 12);
        var fp2 = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(cpuVerts, 12);
        Assert.Equal(fp1, fp2);
        Assert.NotEqual(0UL, fp1);
    }

    [Fact]
    public void Dolphin_gpu_bind_pose_preview_positions_match_cpu_bake()
    {
        const string path = "assets/minecraft/textures/entity/dolphin/dolphin.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, 0.3f, 0f, out var bind, out _));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(bind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(bind, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        const int gpuStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var nCpu = cpuVerts.Length / cpuStride;
        var nGpu = gpuVerts.Length / gpuStride;
        Assert.Equal(nCpu, nGpu);

        const float eps = 2e-4f;
        for (var vi = 0; vi < nCpu; vi++)
        {
            var gpuPreview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(
                new Vector3(gpuVerts[vi * gpuStride], gpuVerts[vi * gpuStride + 1], gpuVerts[vi * gpuStride + 2]));
            var cpuPreview = new Vector3(
                cpuVerts[vi * cpuStride],
                cpuVerts[vi * cpuStride + 1],
                cpuVerts[vi * cpuStride + 2]);
            Assert.True(
                Vector3.Distance(cpuPreview, gpuPreview) <= eps,
                $"vertex {vi}: cpu={cpuPreview} gpu={gpuPreview}");
        }
    }
}
