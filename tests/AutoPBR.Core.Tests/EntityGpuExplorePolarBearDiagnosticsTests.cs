using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Explore GPU bind-pose path for polar bears (128×64 atlas, rotated torso on adult).
/// </summary>
public sealed class EntityGpuExplorePolarBearDiagnosticsTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", 128, 64, false, 0.85f)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", 128, 64, true, 0.55f)]
    public void Gpu_bind_pose_with_palette_matches_cpu_placement_clusters(
        string texturePath,
        int atlasW,
        int atlasH,
        bool isBaby,
        float maxBodyPartGap)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new CleanRoomEntityModelRuntime();
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

        var invBind = new Matrix4x4[mergedBind.Elements.Count];
        for (var i = 0; i < invBind.Length; i++)
        {
            if (!Matrix4x4.Invert(mergedBind.Elements[i].LocalToParent, out invBind[i]))
            {
                invBind[i] = Matrix4x4.Identity;
            }
        }

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm });

        EntityPreviewPlacement.ApplyToPreviewVertices(
            cpuVerts, MinecraftModelBaker.FloatsPerVertex, partIds);
        var gpuPlacement = EntityPreviewPlacement.ApplyToGpuBindVertices(gpuVerts, partIds);

        var cpuBody = PartCentroid(cpuVerts, mergedBind, partIds, MinecraftModelBaker.FloatsPerVertex, id =>
            id.Contains("body", StringComparison.Ordinal));
        var gpuBody = GpuShaderCentroid(
            gpuVerts, mergedBind, partIds, invBind, gpuPlacement.GroundLiftY, id =>
                id.Contains("body", StringComparison.Ordinal));
        Assert.True(cpuBody.HasValue && gpuBody.HasValue, $"{texturePath}: missing body cluster");
        Assert.True(Vector3.Distance(cpuBody.Value, gpuBody.Value) <= 0.06f,
            $"{texturePath} body cpu={cpuBody.Value} gpu={gpuBody.Value}");

        foreach (var suffix in new[] { "leg", "head" })
        {
            var cpuPart = PartCentroid(cpuVerts, mergedBind, partIds, MinecraftModelBaker.FloatsPerVertex, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            var gpuPart = GpuShaderCentroid(
                gpuVerts, mergedBind, partIds, invBind, gpuPlacement.GroundLiftY, id =>
                    id.Contains(suffix, StringComparison.Ordinal) &&
                    !id.Contains("body", StringComparison.Ordinal));
            if (!cpuPart.HasValue || !gpuPart.HasValue)
            {
                continue;
            }

            Assert.True(Vector3.Distance(cpuPart.Value, gpuPart.Value) <= 0.06f,
                $"{texturePath} {suffix} cpu={cpuPart.Value} gpu={gpuPart.Value}");
            if (isBaby)
            {
                var maxGap = suffix == "head" ? 0.75f : maxBodyPartGap;
                Assert.True(Vector3.Distance(cpuBody.Value, cpuPart.Value) <= maxGap,
                    $"{texturePath} cpu {suffix} gap {Vector3.Distance(cpuBody.Value, cpuPart.Value):F3}");
                Assert.True(Vector3.Distance(gpuBody.Value, gpuPart.Value) <= maxGap,
                    $"{texturePath} gpu {suffix} gap {Vector3.Distance(gpuBody.Value, gpuPart.Value):F3}");
            }
        }
    }

    private static Vector3? PartCentroid(
        float[] verts,
        MergedJavaBlockModel mesh,
        List<string> partIds,
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

    private static Vector3? GpuShaderCentroid(
        float[] verts,
        MergedJavaBlockModel mesh,
        List<string> partIds,
        Matrix4x4[] invBind,
        float liftY,
        Func<string, bool> match)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
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
                    var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(verts[i + 12]);
                    var pBind = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                    var bone = Matrix4x4.Multiply(invBind[bi], mesh.Elements[bi].LocalToParent);
                    var skinned = Vector3.Transform(pBind, bone);
                    var preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(skinned);
                    preview.Y += liftY;
                    sum += preview;
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
