using System.Numerics;
using System.Text.Json;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// JVM <c>translateAndRotate</c> render affines vs ModelPart block-stack compose for dolphin fins.
/// </summary>
public sealed class DolphinPartPoseComposeProbeTests
{
    private const string Jvm = "net.minecraft.client.model.animal.dolphin.DolphinModel";

    [Fact]
    public void Dolphin_jvm_render_centers_pectoral_fins_are_laterally_separated_from_body()
    {
        using var reference = LoadReference();
        var centers = JvmRenderReferenceIndex.BuildRenderCenterIndex(reference.RootElement);

        Assert.True(centers.TryGetValue("body", out var bodyCenter));
        Assert.True(centers.TryGetValue("left_fin", out var leftCenter));
        Assert.True(centers.TryGetValue("right_fin", out var rightCenter));

        Assert.True(MathF.Abs(bodyCenter.X) <= 0.5f, $"body center X should be on midline, got {bodyCenter.X}");
        Assert.True(leftCenter.X > bodyCenter.X + 4f,
            $"left_fin should sit laterally right of body: left={leftCenter.X} body={bodyCenter.X}");
        Assert.True(rightCenter.X < bodyCenter.X - 4f,
            $"right_fin should sit laterally left of body: right={rightCenter.X} body={bodyCenter.X}");
        Assert.True(MathF.Abs(leftCenter.X + rightCenter.X) <= 0.05f,
            $"pectoral fins should mirror about midline: left={leftCenter.X} right={rightCenter.X}");
    }

    [Theory]
    [InlineData("left_fin")]
    [InlineData("right_fin")]
    [InlineData("back_fin")]
    public void Dolphin_body_attached_fin_block_stack_render_affine_matches_jvm(string finId)
    {
        using var reference = LoadReference();
        var jvmAffines = JvmRenderReferenceIndex.BuildRenderAffineIndex(reference.RootElement);
        Assert.True(jvmAffines.TryGetValue(finId, out var jvmFinBlock));

        var geometryRoot = LoadRepairedShard();
        Assert.True(TryFindPart(geometryRoot, "body", out var body));
        Assert.True(TryFindPart(geometryRoot, finId, out var fin));

        var blockStackBlock = ComposeBodyAttachedFinBlockStack(body, fin);
        var dist = MatrixDistance(blockStackBlock, jvmFinBlock);
        Assert.True(dist <= 0.001f,
            $"{finId} block-stack block={FormatTranslation(blockStackBlock)} jvm={FormatTranslation(jvmFinBlock)} dist={dist:F4}");
    }

    [Theory]
    [InlineData("left_fin")]
    [InlineData("right_fin")]
    public void Dolphin_pectoral_fin_block_stack_beats_column_on_jvm_render_centers(string finId)
    {
        using var reference = LoadReference();
        var centers = JvmRenderReferenceIndex.BuildRenderCenterIndex(reference.RootElement);
        Assert.True(centers.TryGetValue(finId, out var jvmCenter));

        var geometryRoot = LoadRepairedShard();
        Assert.True(TryFindPart(geometryRoot, "body", out var body));
        Assert.True(TryFindPart(geometryRoot, finId, out var fin));

        var blockCenter = RenderCenterFromCompose(body, fin, useColumn: false);
        var columnCenter = RenderCenterFromCompose(body, fin, useColumn: true);

        var blockDist = Vector3.Distance(blockCenter, jvmCenter);
        var columnDist = Vector3.Distance(columnCenter, jvmCenter);

        Assert.True(blockDist <= 0.08f,
            $"{finId} block-stack center={blockCenter} jvm={jvmCenter} dist={blockDist:F4}");
        Assert.True(blockDist + 0.01f <= columnDist,
            $"{finId} block-stack should beat column on JVM render centers: block={blockDist:F4} column={columnDist:F4}");
    }

    [Fact]
    public void Dolphin_left_fin_render_center_matches_jvm_ground_truth()
    {
        using var reference = LoadReference();
        var centers = JvmRenderReferenceIndex.BuildRenderCenterIndex(reference.RootElement);
        Assert.True(centers.TryGetValue("left_fin", out var jvmCenter));

        var geometryRoot = LoadRepairedShard();
        Assert.True(TryFindPart(geometryRoot, "body", out var body));
        Assert.True(TryFindPart(geometryRoot, "left_fin", out var leftFin));

        var got = RenderCenterFromCompose(body, leftFin, useColumn: false);
        Assert.True(Vector3.Distance(got, jvmCenter) <= 0.08f, $"got={got} jvm={jvmCenter}");
    }

    private static Matrix4x4 ComposeBodyAttachedFinBlockStack(JsonElement body, JsonElement fin)
    {
        Assert.True(EntityModelRuntime.TryComposePartRenderLocalBlock(
            body.GetProperty("pose"), out var bodyLocal, out _));
        Assert.True(EntityModelRuntime.TryComposePartRenderLocalBlock(
            fin.GetProperty("pose"), out var finLocal, out _));
        return Matrix4x4.Multiply(finLocal, bodyLocal);
    }

    private static Vector3 RenderCenterFromCompose(JsonElement body, JsonElement fin, bool useColumn)
    {
        if (useColumn)
        {
            Assert.True(EntityModelRuntime.TryComposePartPosePublic(
                body.GetProperty("pose"), Matrix4x4.Identity, out var bodyWorld));
            Assert.True(EntityModelRuntime.TryComposeColumnPartPose(
                fin.GetProperty("pose"), bodyWorld, out var columnWorld, out _));
            return TransformCuboidCenter(fin, EntityModelRuntime.TexelRowAffineToBlock(columnWorld));
        }

        return TransformCuboidCenter(fin, ComposeBodyAttachedFinBlockStack(body, fin));
    }

    private static Vector3 TransformCuboidCenter(JsonElement fin, Matrix4x4 finWorldBlock)
    {
        var cuboid = fin.GetProperty("cuboids")[0];
        var from = cuboid.GetProperty("from");
        var to = cuboid.GetProperty("to");
        var cx = (from[0].GetSingle() + to[0].GetSingle()) * 0.5f;
        var cy = (from[1].GetSingle() + to[1].GetSingle()) * 0.5f;
        var cz = (from[2].GetSingle() + to[2].GetSingle()) * 0.5f;
        var block = new Vector3(cx / 16f, cy / 16f, cz / 16f);
        return Vector3.Transform(block, finWorldBlock) * 16f;
    }

    private static JsonDocument LoadReference()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var path = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{Jvm}.json");
        Assert.True(File.Exists(path), $"Missing JVM reference export: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonElement LoadRepairedShard()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        return GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
    }

    private static bool TryFindPart(JsonElement geometryRoot, string partId, out JsonElement part)
    {
        part = default;
        if (!geometryRoot.TryGetProperty("roots", out var roots))
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (WalkPart(root, partId, out part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WalkPart(JsonElement node, string partId, out JsonElement part)
    {
        part = default;
        if (node.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
        {
            part = node;
            return true;
        }

        if (node.TryGetProperty("children", out var children))
        {
            foreach (var ch in children.EnumerateArray())
            {
                if (WalkPart(ch, partId, out part))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float MatrixDistance(Matrix4x4 a, Matrix4x4 b)
    {
        var sum = 0f;
        sum += MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13) + MathF.Abs(a.M14 - b.M14);
        sum += MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23) + MathF.Abs(a.M24 - b.M24);
        sum += MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) + MathF.Abs(a.M34 - b.M34);
        sum += MathF.Abs(a.M41 - b.M41) + MathF.Abs(a.M42 - b.M42) + MathF.Abs(a.M43 - b.M43) + MathF.Abs(a.M44 - b.M44);
        return sum;
    }

    private static string FormatTranslation(Matrix4x4 m) => $"[{m.M41:R},{m.M42:R},{m.M43:R}]";

    private static class JvmRenderReferenceIndex
    {
        public static Dictionary<string, Matrix4x4> BuildRenderAffineIndex(JsonElement referenceRoot)
        {
            var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            if (!referenceRoot.TryGetProperty("renderPartAffines", out var affines))
            {
                return map;
            }

            foreach (var entry in affines.EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString() ?? "";
                if (id.Length == 0)
                {
                    continue;
                }

                map[id] = ReadRowMajorMatrix(entry.GetProperty("matrixRowMajor"));
            }

            return map;
        }

        public static Dictionary<string, Vector3> BuildRenderCenterIndex(JsonElement referenceRoot)
        {
            var map = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            if (!referenceRoot.TryGetProperty("renderCuboidCenters", out var centers))
            {
                return map;
            }

            foreach (var entry in centers.EnumerateArray())
            {
                var id = entry.GetProperty("partId").GetString() ?? "";
                if (id.Length == 0)
                {
                    continue;
                }

                var c = entry.GetProperty("renderCenterTexel");
                map[id] = new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
            }

            return map;
        }

        private static Matrix4x4 ReadRowMajorMatrix(JsonElement rows)
        {
            var r0 = rows[0];
            var r1 = rows[1];
            var r2 = rows[2];
            var r3 = rows[3];
            return new Matrix4x4(
                r0[0].GetSingle(), r0[1].GetSingle(), r0[2].GetSingle(), r0[3].GetSingle(),
                r1[0].GetSingle(), r1[1].GetSingle(), r1[2].GetSingle(), r1[3].GetSingle(),
                r2[0].GetSingle(), r2[1].GetSingle(), r2[2].GetSingle(), r2[3].GetSingle(),
                r3[0].GetSingle(), r3[1].GetSingle(), r3[2].GetSingle(), r3[3].GetSingle());
        }
    }
}
