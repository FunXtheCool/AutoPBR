using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class BlockModelUvGoldenTests
{
    [Fact]
    public void BlockOrItemBaseline_single_face_uv_spans_full_tile()
    {
        using var fixture = new BlockModelMergerTests.BlockModelFixture();
        fixture.Write(
            "assets/minecraft/models/block/stone_panel.json",
            """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": {
                    "north": { "texture": "#tex", "uv": [0, 0, 16, 16] }
                  }
                }
              ],
              "textures": { "tex": "minecraft:block/stone" }
            }
            """);

        var source = new DirectoryAssetSource(fixture.Root);
        Assert.True(MinecraftModelMerger.TryMerge(
            source,
            "assets/minecraft/models/block/stone_panel.json",
            out var merged));
        const string texPath = "assets/minecraft/textures/block/stone.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [texPath] = 0 };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [texPath] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var uvs = CollectAllUvs(verts);
        Assert.Equal(4, uvs.Count);
        var minU = uvs.Min(u => u.X);
        var maxU = uvs.Max(u => u.X);
        var minV = uvs.Min(u => u.Y);
        var maxV = uvs.Max(u => u.Y);
        Assert.InRange(maxU - minU, 0.99f, 1.01f);
        Assert.InRange(maxV - minV, 0.99f, 1.01f);
    }

    [Fact]
    public void BlockOrItemBaseline_policy_applies_to_block_json_models()
    {
        using var fixture = new BlockModelMergerTests.BlockModelFixture();
        fixture.Write(
            "assets/minecraft/models/block/stone.json",
            """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": {
                    "north": { "texture": "#tex", "uv": [0, 0, 16, 16] }
                  }
                }
              ],
              "textures": { "tex": "minecraft:block/stone" }
            }
            """);

        var source = new DirectoryAssetSource(fixture.Root);
        Assert.True(MinecraftModelMerger.TryMerge(
            source,
            "assets/minecraft/models/block/stone.json",
            out var merged));

        var policy = PreviewUvBakePolicy.Resolve(merged);
        Assert.True(policy.FlipV);
        Assert.True(policy.PreserveDirectionalBounds);
        Assert.True(policy.ReverseFaceWinding);
        Assert.False(policy.MapJavaCuboidFaceCorners);
    }

    [Fact]
    public void BlockJson_rotated_face_tangent_follows_final_uv_u_axis()
    {
        using var fixture = new BlockModelMergerTests.BlockModelFixture();
        fixture.Write(
            "assets/minecraft/models/block/rotated_panel.json",
            """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": {
                    "north": { "texture": "#tex", "uv": [0, 0, 16, 16], "rotation": 90 }
                  }
                }
              ],
              "textures": { "tex": "minecraft:block/stone" }
            }
            """);

        var source = new DirectoryAssetSource(fixture.Root);
        Assert.True(MinecraftModelMerger.TryMerge(
            source,
            "assets/minecraft/models/block/rotated_panel.json",
            out var merged));
        const string texPath = "assets/minecraft/textures/block/stone.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [texPath] = 0 };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [texPath] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var i = 0; i < verts.Length / stride; i++)
        {
            var o = i * stride;
            var tangent = new Vector3(verts[o + 8], verts[o + 9], verts[o + 10]);
            AssertVectorNear(new Vector3(0f, -1f, 0f), tangent);
            Assert.Equal(-1f, verts[o + 11]);
        }
    }

    private static List<System.Numerics.Vector2> CollectAllUvs(float[] verts)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var uvs = new List<System.Numerics.Vector2>(verts.Length / stride);
        for (var i = 0; i < verts.Length / stride; i++)
        {
            uvs.Add(new System.Numerics.Vector2(verts[i * stride + 6], verts[i * stride + 7]));
        }

        return uvs;
    }

    private static void AssertVectorNear(Vector3 expected, Vector3 actual, float tolerance = 1e-5f)
    {
        Assert.InRange(MathF.Abs(actual.X - expected.X), 0f, tolerance);
        Assert.InRange(MathF.Abs(actual.Y - expected.Y), 0f, tolerance);
        Assert.InRange(MathF.Abs(actual.Z - expected.Z), 0f, tolerance);
    }
}
