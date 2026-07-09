using System.Numerics;
using System.Text.Json;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class ModelPartTranslateAndRotateProbeTests
{
    [Fact]
    public void Cold_cow_jvm_render_body_affine_matches_translate_then_rotate_block_chain()
    {
        var bodyWorldBlock = MatrixFromRowMajor(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            0, 0.3125f, 0.125f, 1);

        var local = SeparatedTranslateRotateBlock(T(0f, 5f / 16f, 2f / 16f), Rx(MathF.PI / 2f));
        Assert.True(MatrixDistance(local, bodyWorldBlock) <= 0.001f,
            $"local={Format(local)} jvm={Format(bodyWorldBlock)}");
    }

    private static Matrix4x4 SeparatedTranslateRotateBlock(Matrix4x4 translation, Matrix4x4 rotation) =>
        new(
            rotation.M11, rotation.M12, rotation.M13, rotation.M14,
            rotation.M21, rotation.M22, rotation.M23, rotation.M24,
            rotation.M31, rotation.M32, rotation.M33, rotation.M34,
            translation.M41, translation.M42, translation.M43, translation.M44);

    private static string Format(Matrix4x4 m) =>
        $"[{m.M41:R},{m.M42:R},{m.M43:R}]";

    [Fact]
    public void Cold_cow_jvm_render_horn_affine_matches_translate_then_rotate_block_chain()
    {
        var headWorldBlock = MatrixFromRowMajor(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0.25f, -0.5f, 1);
        var hornWorldBlock = MatrixFromRowMajor(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            -0.28125f, 0.09375f, -0.71875f, 1);

        Matrix4x4? bestLocal = null;
        var bestLabel = "";
        var bestDist = float.MaxValue;
        foreach (var order in new[] { "TR", "RT" })
        {
            foreach (var chainName in new[] { "headLocal", "localHead" })
            {
                var hornLocal = order == "TR"
                    ? SeparatedTranslateRotateBlock(T(-4.5f / 16f, -2.5f / 16f, -3.5f / 16f), Rx(MathF.PI / 2f))
                    : Mul(Rx(MathF.PI / 2f), T(-4.5f / 16f, -2.5f / 16f, -3.5f / 16f));
                var chain = chainName == "headLocal"
                    ? Mul(headWorldBlock, hornLocal)
                    : Mul(hornLocal, headWorldBlock);
                var d = MatrixDistance(chain, hornWorldBlock);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestLocal = hornLocal;
                    bestLabel = $"{order}/{chainName}";
                }
            }
        }

        Assert.True(bestDist <= 0.001f, $"best={bestLabel} bestDist={bestDist:F4}");

        var cuboidTexel = new Vector3(-0.5f, -1.5f, 0.5f);
        var jvmExpected = new Vector3(-5f, 1.000005603f, -13.00000191f);
        var got = TransformTexelViaBlockChain(cuboidTexel, hornWorldBlock);
        Assert.True(Vector3.Distance(got, jvmExpected) <= 0.05f, $"got={got} expected={jvmExpected}");
    }

    [Fact]
    public void Production_compose_cold_cow_horn_world_matches_jvm_render_affine()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        const string jvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json")));
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);

        Assert.True(TryFindPart(geometryRoot, "head", out var head));
        Assert.True(TryFindPart(geometryRoot, "right_horn", out var horn));

        Assert.True(EntityModelRuntime.TryComposePartPosePublic(
            head.GetProperty("pose"), Matrix4x4.Identity, out var headWorld));
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(
            horn.GetProperty("pose"), headWorld, out var hornWorld));

        var jvmHornBlock = MatrixFromRowMajor(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            -0.28125f, 0.09375f, -0.71875f, 1);
        var jvmHornTexel = EntityModelRuntime.BlockRowAffineToTexel(jvmHornBlock);

        var headBlock = SeparatedTranslateRotateBlock(T(0f, 4f / 16f, -8f / 16f), Matrix4x4.Identity);
        var probeHead = MatrixFromRowMajor(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0.25f, -0.5f, 1);
        Assert.True(MatrixDistance(headBlock, probeHead) <= 0.0001f,
            $"headBlock mismatch probeHead d={MatrixDistance(headBlock, probeHead)}");
        var hornLocal = SeparatedTranslateRotateBlock(T(-4.5f / 16f, -2.5f / 16f, -3.5f / 16f), Rx(MathF.PI / 2f));
        var chain = Mul(hornLocal, probeHead);
        Assert.True(MatrixDistance(chain, jvmHornBlock) <= 0.001f,
            $"probe chain row4={Format(chain)} jvm={Format(jvmHornBlock)}");
        var manual = EntityModelRuntime.BlockRowAffineToTexel(chain);
        Assert.True(MatrixDistance(manual, jvmHornTexel) <= 0.05f,
            $"manual={Format(manual)} jvm={Format(jvmHornTexel)} production={Format(hornWorld)}");
        Assert.True(MatrixDistance(hornWorld, jvmHornTexel) <= 0.05f,
            $"production={Format(hornWorld)} jvm={Format(jvmHornTexel)} manual={Format(manual)}");
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

    private static Matrix4x4 MatrixFromRowMajor(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44) =>
        new(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);

    private static Vector3 TransformTexelViaBlockChain(Vector3 texel, Matrix4x4 blockChain)
    {
        var block = texel / 16f;
        return Vector3.Transform(block, blockChain) * 16f;
    }

    private static Matrix4x4 Mul(Matrix4x4 a, Matrix4x4 b) => Matrix4x4.Multiply(a, b);

    private static Matrix4x4 T(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

    private static Matrix4x4 Rx(float r) => Matrix4x4.CreateRotationX(r);
}
