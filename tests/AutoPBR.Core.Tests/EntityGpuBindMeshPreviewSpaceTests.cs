using System.Numerics;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Animation-off Explore path bakes preview-space positions into the GPU bind VBO (same as CPU rebake).
/// </summary>
public sealed class EntityGpuBindMeshPreviewSpaceTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 64, 64)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", 64, 64)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", 128, 64)]
    public void Preview_space_bake_matches_cpu_placement_clusters(string texturePath, int atlasW, int atlasH)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mergedBind));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedBind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (atlasW, atlasH);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            mergedBind, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mergedBind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule!, texturePath, stem, stem.Contains("baby", StringComparison.Ordinal), out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm });

        EntityPreviewPlacement.ApplyToPreviewVertices(
            cpuVerts, MinecraftModelBaker.FloatsPerVertex, partIds);
        var gpuPlacement = EntityPreviewPlacement.ApplyToGpuBindVertices(gpuVerts, partIds);
        EntityGpuBindMeshPreviewSpaceTransform.BakeIntoVertices(gpuVerts, gpuPlacement.GroundLiftY);
        var gpuPreviewVerts = EntityGpuBindMeshPreviewSpaceTransform.ToPreviewMeshLayout(gpuVerts);

        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        Assert.Equal(cpuVerts.Length, gpuPreviewVerts.Length);
        var maxErr = 0f;
        for (var i = 0; i + cpuStride - 1 < cpuVerts.Length; i += cpuStride)
        {
            var cpuPos = new Vector3(cpuVerts[i], cpuVerts[i + 1], cpuVerts[i + 2]);
            var gpuPos = new Vector3(gpuPreviewVerts[i], gpuPreviewVerts[i + 1], gpuPreviewVerts[i + 2]);
            maxErr = MathF.Max(maxErr, Vector3.Distance(cpuPos, gpuPos));
        }

        Assert.True(maxErr <= 0.02f, $"{texturePath} max vertex err={maxErr:F4}");
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 0.2f, 1.1f)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", 0f, 2.5f)]
    public void Animated_skin_bake_matches_cpu_rebake(string texturePath, float idle, float anim)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, idle, 0f, out var mergedBind));
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            Profile26,
            idle,
            anim,
            out var mergedAnim,
            applyGeometryIrSetupAnimMotion: true));

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
            mergedAnim,
            "minecraft",
            pathToIdx,
            texSizes,
            out var cpuVerts,
            out _,
            out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mergedBind,
            "minecraft",
            pathToIdx,
            texSizes,
            out var gpuBindVerts,
            out _,
            out _));

        var invBind = new Matrix4x4[mergedBind.Elements.Count];
        for (var i = 0; i < invBind.Length; i++)
        {
            if (!Matrix4x4.Invert(mergedBind.Elements[i].LocalToParent, out invBind[i]))
            {
                invBind[i] = Matrix4x4.Identity;
            }
        }

        Span<Matrix4x4> bones = stackalloc Matrix4x4[EntityGpuSkinningLimits.MaxBones];
        for (var i = 0; i < mergedAnim.Elements.Count; i++)
        {
            bones[i] = Matrix4x4.Multiply(invBind[i], mergedAnim.Elements[i].LocalToParent);
        }

        var gpuPreviewVerts = EntityGpuBindMeshPreviewSpaceTransform.SkinAndBakeToPreviewLayout(
            gpuBindVerts,
            bones[..mergedAnim.Elements.Count],
            mergedAnim.Elements.Count,
            meshSpaceLiftY: 0f);

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        Assert.Equal(cpuVerts.Length, gpuPreviewVerts.Length);
        var maxErr = 0f;
        for (var i = 0; i + stride - 1 < cpuVerts.Length; i += stride)
        {
            var cpuPos = new Vector3(cpuVerts[i], cpuVerts[i + 1], cpuVerts[i + 2]);
            var gpuPos = new Vector3(gpuPreviewVerts[i], gpuPreviewVerts[i + 1], gpuPreviewVerts[i + 2]);
            maxErr = MathF.Max(maxErr, Vector3.Distance(cpuPos, gpuPos));
        }

        Assert.True(maxErr <= 0.02f, $"{texturePath} max vertex err={maxErr:F4}");
    }

    private static Vector3? PartCentroid(
        float[] verts,
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds,
        int stride,
        Func<string, bool> match)
    {
        var sum = Vector3.Zero;
        var count = 0;
        var vertexBase = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountFaces(mesh.Elements[e]) * 4;
            if (match(partIds[e]))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vertexBase + v) * stride;
                    sum += new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                    count++;
                }
            }

            vertexBase += vertCount;
        }

        return count > 0 ? sum / count : null;
    }

    private static int CountFaces(ModelElement el)
    {
        var n = 0;
        foreach (var name in new[] { "north", "south", "west", "east", "up", "down" })
        {
            if (el.Faces.ContainsKey(name))
            {
                n++;
            }
        }

        return n;
    }
}
