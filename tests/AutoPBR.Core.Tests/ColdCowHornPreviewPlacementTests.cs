using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Cold cow horns must render on the head cluster in preview space (GPU bind + CPU), not inside the rotated body.
/// </summary>
public sealed class ColdCowHornPreviewPlacementTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    private const string TexturePath = "assets/minecraft/textures/entity/cow/cow_cold.png";
    private const string Jvm = "net.minecraft.client.model.animal.cow.ColdCowModel";

    [Fact]
    public void Catalog_horn_cuboid_centroids_cluster_with_head_not_body_in_cpu_and_gpu_preview_space()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new CleanRoomEntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var merged));

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = Jvm });

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            merged, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            merged, "minecraft", pathToIdx, texSizes, out var gpuBind, out _, out _));

        EntityPreviewPlacement.ApplyToPreviewVertices(cpuVerts, MinecraftModelBaker.FloatsPerVertex, partIds);
        var gpuPlacement = EntityPreviewPlacement.ApplyToGpuBindVertices(gpuBind, partIds);

        var cpuHead = PartCentroid(cpuVerts, merged, partIds, MinecraftModelBaker.FloatsPerVertex, id => id == "head");
        var cpuHorn = PartCentroid(cpuVerts, merged, partIds, MinecraftModelBaker.FloatsPerVertex, id => id.Contains("horn", StringComparison.Ordinal));
        var cpuBody = PartCentroid(cpuVerts, merged, partIds, MinecraftModelBaker.FloatsPerVertex, id => id == "body");
        Assert.True(cpuHead.HasValue && cpuHorn.HasValue && cpuBody.HasValue);

        var gpuHead = GpuBindCentroid(gpuBind, merged, partIds, gpuPlacement.GroundLiftY, id => id == "head");
        var gpuHorn = GpuBindCentroid(gpuBind, merged, partIds, gpuPlacement.GroundLiftY, id => id.Contains("horn", StringComparison.Ordinal));
        var gpuBody = GpuBindCentroid(gpuBind, merged, partIds, gpuPlacement.GroundLiftY, id => id == "body");
        Assert.True(gpuHead.HasValue && gpuHorn.HasValue && gpuBody.HasValue);

        AssertHornNearHeadNotBody("cpu", cpuHead.Value, cpuHorn.Value, cpuBody.Value);
        AssertHornNearHeadNotBody("gpu", gpuHead.Value, gpuHorn.Value, gpuBody.Value);

        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{Jvm}.json")));
        var refHornPreview = JvmRenderCuboidPreviewCentroid(reference.RootElement, "right_horn", gpuPlacement.GroundLiftY);
        Assert.NotNull(refHornPreview);
        var meshHornPreview = HornElementPreviewCentroid(merged, partIds, gpuPlacement.GroundLiftY);
        Assert.NotNull(meshHornPreview);
        Assert.True(Vector3.Distance(refHornPreview.Value, meshHornPreview.Value) <= 0.08f,
            $"mesh horn cuboid vs JVM renderCenterTexel: ref={refHornPreview.Value} mesh={meshHornPreview.Value}");
    }

    private static Vector3? JvmRenderCuboidPreviewCentroid(JsonElement referenceRoot, string partId, float liftY)
    {
        if (!referenceRoot.TryGetProperty("renderCuboidCenters", out var centers) ||
            centers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in centers.EnumerateArray())
        {
            if (!entry.TryGetProperty("partId", out var pidEl) ||
                !string.Equals(pidEl.GetString(), partId, StringComparison.Ordinal) ||
                !entry.TryGetProperty("renderCenterTexel", out var t) ||
                t.GetArrayLength() < 3)
            {
                continue;
            }

            var model = new Vector3(
                (float)t[0].GetDouble(),
                (float)t[1].GetDouble(),
                (float)t[2].GetDouble());
            var preview = Vector3.Transform(model, CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale);
            preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(preview);
            preview.Y += liftY;
            return preview;
        }

        return null;
    }

    private static Vector3? HornElementPreviewCentroid(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        float liftY)
    {
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            if (!partIds[e].Contains("horn", StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[e];
            var center = new Vector3(
                (el.From[0] + el.To[0]) * 0.5f,
                (el.From[1] + el.To[1]) * 0.5f,
                (el.From[2] + el.To[2]) * 0.5f);
            var preview = Vector3.Transform(center, el.LocalToParent);
            preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(preview);
            preview.Y += liftY;
            return preview;
        }

        return null;
    }

    private static void AssertHornNearHeadNotBody(string path, Vector3 head, Vector3 horn, Vector3 body)
    {
        var hornHeadDist = Vector3.Distance(horn, head);
        var hornBodyDist = Vector3.Distance(horn, body);
        Assert.True(hornHeadDist < hornBodyDist,
            $"{path}: horn should be closer to head than body; horn={horn} head={head} body={body} dHead={hornHeadDist:F3} dBody={hornBodyDist:F3}");
        Assert.True(hornHeadDist <= 8f,
            $"{path}: horn too far from head; horn={horn} head={head} d={hornHeadDist:F3}");
        Assert.True(MathF.Abs(horn.Z - head.Z) <= 6f,
            $"{path}: horn Z should track head Z cluster; hornZ={horn.Z:F3} headZ={head.Z:F3} bodyZ={body.Z:F3}");
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

    private static Vector3? GpuBindCentroid(
        float[] bindSkinned,
        MergedJavaBlockModel mesh,
        List<string> partIds,
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
                    var p = new Vector3(bindSkinned[i], bindSkinned[i + 1], bindSkinned[i + 2]);
                    p = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p);
                    p.Y += liftY;
                    sum += p;
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
