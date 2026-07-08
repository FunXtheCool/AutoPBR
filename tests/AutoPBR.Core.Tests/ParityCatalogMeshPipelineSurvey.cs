using System.Numerics;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Shared helpers for catalog-wide preview mesh pipeline surveys (determinism, CPU/GPU bind parity).
/// </summary>
internal static class ParityCatalogMeshPipelineSurvey
{
    internal readonly record struct CpuGpuBindResult(
        bool Succeeded,
        float MaxVertexDelta,
        string? Failure);

    internal static MinecraftNativeProfile DefaultProfile26() =>
        new(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));

    internal static bool TryAssertRuntimeDeterminism(
        string path,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        bool applyGeometryIrSetupAnimMotion,
        out string? failure)
    {
        failure = null;
        var runtime = EntityModelRuntimeFactory.Create();
        if (!runtime.TryBuildStaticMesh(
                path,
                profile,
                idlePhase01,
                animationTimeSeconds,
                out var a,
                out _,
                applyGeometryIrSetupAnimMotion,
                pairDoubleChestPreviewHalves: false))
        {
            failure = "first build failed";
            return false;
        }

        if (!runtime.TryBuildStaticMesh(
                path,
                profile,
                idlePhase01,
                animationTimeSeconds,
                out var b,
                out _,
                applyGeometryIrSetupAnimMotion,
                pairDoubleChestPreviewHalves: false))
        {
            failure = "second build failed";
            return false;
        }

        var cmp = GeometryIrMeshParityComparer.Compare(a, b, tolerance: 1e-5f);
        if (!cmp.IsMatch)
        {
            failure = cmp.Message ?? "meshes differ";
            return false;
        }

        return true;
    }

    internal static CpuGpuBindResult MeasureCpuGpuBindParity(
        string path,
        MinecraftNativeProfile profile,
        float idlePhase01 = 0f,
        float animationTimeSeconds = 0f,
        bool applyGeometryIrSetupAnimMotion = false,
        float vertexTolerance = 2e-4f)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        if (!runtime.TryBuildStaticMesh(
                path,
                profile,
                idlePhase01,
                animationTimeSeconds,
                out var bindMerged,
                out _,
                applyGeometryIrSetupAnimMotion,
                pairDoubleChestPreviewHalves: false))
        {
            return new CpuGpuBindResult(false, float.MaxValue, "bind mesh build failed");
        }

        if (!runtime.TryBuildStaticMesh(
                path,
                profile,
                idlePhase01,
                animationTimeSeconds,
                out var animMerged,
                out _,
                applyGeometryIrSetupAnimMotion,
                pairDoubleChestPreviewHalves: false))
        {
            return new CpuGpuBindResult(false, float.MaxValue, "anim mesh build failed");
        }

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(bindMerged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            var stem = Path.GetFileNameWithoutExtension(p);
            var rule = EntityTextureParityCatalog.ResolveRule(p, stem);
            var w = rule?.GeometryIrTextureWidth is > 0 and var rw ? rw : 64;
            var h = rule?.GeometryIrTextureHeight is > 0 and var rh ? rh : 64;
            texSizes[p] = (w, h);
        }

        if (!MinecraftModelBaker.TryBake(
                animMerged,
                "minecraft",
                pathToIdx,
                texSizes,
                out var cpuVerts,
                out _,
                out _))
        {
            return new CpuGpuBindResult(false, float.MaxValue, "cpu bake failed");
        }

        if (!MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
                bindMerged,
                "minecraft",
                pathToIdx,
                texSizes,
                out var gpuVerts,
                out _,
                out _))
        {
            return new CpuGpuBindResult(false, float.MaxValue, "gpu bind bake failed");
        }

        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        const int gpuStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var nCpu = cpuVerts.Length / cpuStride;
        var nGpu = gpuVerts.Length / gpuStride;
        if (nCpu != nGpu)
        {
            return new CpuGpuBindResult(false, float.MaxValue, $"vertex count cpu={nCpu} gpu={nGpu}");
        }

        var invBind = InvertBindPoses(bindMerged);
        var maxDelta = 0f;
        for (var vi = 0; vi < nCpu; vi++)
        {
            var bi = BitConverter.SingleToInt32Bits(gpuVerts[vi * gpuStride + 12]);
            if (bi < 0 || bi >= invBind.Length)
            {
                return new CpuGpuBindResult(false, float.MaxValue, $"bone index {bi} out of range");
            }

            var pBind = new Vector3(
                gpuVerts[vi * gpuStride],
                gpuVerts[vi * gpuStride + 1],
                gpuVerts[vi * gpuStride + 2]);
            var m = animMerged.Elements[bi].LocalToParent;
            var texel = Vector3.Transform(Vector3.Transform(pBind, invBind[bi]), m);
            var actual = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(texel);
            var expected = new Vector3(
                cpuVerts[vi * cpuStride],
                cpuVerts[vi * cpuStride + 1],
                cpuVerts[vi * cpuStride + 2]);
            maxDelta = MathF.Max(maxDelta, Vector3.Distance(expected, actual));
            if (maxDelta > vertexTolerance)
            {
                return new CpuGpuBindResult(
                    false,
                    maxDelta,
                    $"vertex {vi} bone {bi}: cpu={expected} gpu={actual}");
            }
        }

        return new CpuGpuBindResult(true, maxDelta, null);
    }

    private static Matrix4x4[] InvertBindPoses(MergedJavaBlockModel bindMerged)
    {
        var n = bindMerged.Elements.Count;
        var inv = new Matrix4x4[n];
        for (var i = 0; i < n; i++)
        {
            if (!Matrix4x4.Invert(bindMerged.Elements[i].LocalToParent, out inv[i]))
            {
                inv[i] = Matrix4x4.Identity;
            }
        }

        return inv;
    }
}
