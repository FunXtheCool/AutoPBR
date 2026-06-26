using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Baby goat horns use <c>PartPose.offsetAndRotation</c> under a pitched head; column <c>Er×T</c> compose must match JVM render affines.
/// </summary>
public sealed class BabyGoatHornPreviewPlacementTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    private const string TexturePath = "assets/minecraft/textures/entity/goat/goat_baby.png";
    private const string Jvm = "net.minecraft.client.model.animal.goat.BabyGoatModel";

    [Fact]
    public void Horn_part_compose_matches_jvm_render_affine_under_pitched_head()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{Jvm}.json")));
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);

        Assert.True(TryFindPart(geometryRoot, "head", out var head));
        Assert.True(TryFindPart(geometryRoot, "right_horn", out var horn));

        Assert.True(CleanRoomEntityModelRuntime.TryComposePartPosePublic(
            head.GetProperty("pose"), Matrix4x4.Identity, out var headWorld, "head"));
        Assert.True(CleanRoomEntityModelRuntime.TryComposePartPosePublic(
            horn.GetProperty("pose"), headWorld, out var hornWorld, "right_horn"));

        Matrix4x4? jvmHorn = null;
        foreach (var entry in reference.RootElement.GetProperty("renderPartAffines").EnumerateArray())
        {
            if (entry.GetProperty("id").GetString() != "right_horn")
            {
                continue;
            }

            jvmHorn = ParseRowMajor(entry.GetProperty("matrixRowMajor"));
        }

        Assert.NotNull(jvmHorn);
        var jvmTexel = CleanRoomEntityModelRuntime.BlockRowAffineToTexel(jvmHorn.Value);
        Assert.True(MatrixDistance(hornWorld, jvmTexel) <= 0.05f,
            $"horn={Format(hornWorld)} jvm={Format(jvmTexel)}");
    }

    [Fact]
    public void Catalog_horn_cuboid_centroids_cluster_with_head_not_body_in_preview_space()
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

        EntityPreviewPlacement.ApplyToPreviewVertices(cpuVerts, MinecraftModelBaker.FloatsPerVertex, partIds);

        var cpuHead = PartCentroid(cpuVerts, merged, partIds, id => id == "head" || id == "HeadMain");
        var cpuHorn = PartCentroid(cpuVerts, merged, partIds, id => id.Contains("horn", StringComparison.Ordinal));
        var cpuBody = PartCentroid(cpuVerts, merged, partIds, id => id == "body");
        Assert.True(cpuHead.HasValue && cpuHorn.HasValue && cpuBody.HasValue);

        var hornHeadDist = Vector3.Distance(cpuHorn.Value, cpuHead.Value);
        var hornBodyDist = Vector3.Distance(cpuHorn.Value, cpuBody.Value);
        Assert.True(hornHeadDist < hornBodyDist,
            $"horn should be closer to head; horn={cpuHorn.Value} head={cpuHead.Value} body={cpuBody.Value}");
        Assert.True(hornHeadDist <= 0.35f,
            $"horn too far from head cluster; horn={cpuHorn.Value} head={cpuHead.Value} d={hornHeadDist:F3}");
    }

    private static bool TryFindPart(JsonElement root, string id, out JsonElement part)
    {
        part = default;
        if (!root.TryGetProperty("roots", out var roots))
        {
            return false;
        }

        foreach (var r in roots.EnumerateArray())
        {
            if (Walk(r, id, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Walk(JsonElement node, string id, out JsonElement part)
    {
        part = default;
        if (node.TryGetProperty("id", out var idEl) && idEl.GetString() == id)
        {
            part = node;
            return true;
        }

        if (!node.TryGetProperty("children", out var kids))
        {
            return false;
        }

        foreach (var kid in kids.EnumerateArray())
        {
            if (Walk(kid, id, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static Matrix4x4 ParseRowMajor(JsonElement rows)
    {
        var m = new float[16];
        var i = 0;
        foreach (var row in rows.EnumerateArray())
        {
            foreach (var v in row.EnumerateArray())
            {
                m[i++] = (float)v.GetDouble();
            }
        }

        return new Matrix4x4(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]);
    }

    private static float MatrixDistance(Matrix4x4 a, Matrix4x4 b)
    {
        float s = 0;
        s += MathF.Abs(a.M41 - b.M41) + MathF.Abs(a.M42 - b.M42) + MathF.Abs(a.M43 - b.M43);
        s += MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13);
        s += MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23);
        s += MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33);
        return s;
    }

    private static string Format(Matrix4x4 m) => $"[{m.M41:R},{m.M42:R},{m.M43:R}]";

    private static Vector3? PartCentroid(
        float[] verts,
        MergedJavaBlockModel mesh,
        List<string> partIds,
        Func<string, bool> match)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
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
