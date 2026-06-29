using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>
/// UV bake goldens against pinned vanilla 26.1.2 block model JSON under <c>Data/vanilla-26.1.2</c>.
/// </summary>
public sealed class BlockVanillaJsonUvGoldenTests
{
    [Fact]
    public void Pinned_stone_cube_west_face_uv_spans_full_tile()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string modelPath = "assets/minecraft/models/block/stone.json";
        Assert.True(MinecraftModelMerger.TryMerge(source, modelPath, out var merged));

        const string texPath = "assets/minecraft/textures/block/stone.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [texPath] = 0 };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [texPath] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var span = MeasureUvSpan(verts);
        Assert.InRange(span.X, 0.99f, 1.01f);
        Assert.InRange(span.Y, 0.99f, 1.01f);
    }

    [Fact]
    public void Pinned_door_bottom_left_west_face_matches_vanilla_uv_corners()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string modelPath = "assets/minecraft/models/block/acacia_door_bottom_left.json";
        Assert.True(MinecraftModelMerger.TryMerge(source, modelPath, out var merged));

        var bottomTex = "assets/minecraft/textures/block/acacia_door_bottom.png";
        var topTex = "assets/minecraft/textures/block/acacia_door_top.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = 0,
            [topTex] = 1,
        };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = (16, 16),
            [topTex] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var westUvs = CollectFaceUvs(verts, faceNormal: new Vector3(-1f, 0f, 0f));
        Assert.Equal(4, westUvs.Count);
        var minU = westUvs.Min(u => u.X);
        var maxU = westUvs.Max(u => u.X);
        var minV = westUvs.Min(u => u.Y);
        var maxV = westUvs.Max(u => u.Y);
        Assert.InRange(maxU - minU, 0.99f, 1.01f);
        Assert.InRange(maxV - minV, 0.99f, 1.01f);
    }

    [Fact]
    public void Pinned_door_top_left_west_face_matches_vanilla_uv_corners()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string modelPath = "assets/minecraft/models/block/acacia_door_top_left.json";
        Assert.True(MinecraftModelMerger.TryMerge(source, modelPath, out var merged));

        var bottomTex = "assets/minecraft/textures/block/acacia_door_bottom.png";
        var topTex = "assets/minecraft/textures/block/acacia_door_top.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = 0,
            [topTex] = 1,
        };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = (16, 16),
            [topTex] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var westUvs = CollectFaceUvs(verts, faceNormal: new Vector3(-1f, 0f, 0f));
        Assert.Equal(4, westUvs.Count);
        var minU = westUvs.Min(u => u.X);
        var maxU = westUvs.Max(u => u.X);
        var minV = westUvs.Min(u => u.Y);
        var maxV = westUvs.Max(u => u.Y);
        Assert.InRange(maxU - minU, 0.99f, 1.01f);
        Assert.InRange(maxV - minV, 0.99f, 1.01f);
    }

    [Fact]
    public void Rebaked_door_pair_spans_two_block_heights_on_Y()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_bottom.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.True(BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(texturePath, ns, ref merged));

        var bottomTex = "assets/minecraft/textures/block/acacia_door_bottom.png";
        var topTex = "assets/minecraft/textures/block/acacia_door_top.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = 0,
            [topTex] = 1,
        };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = (16, 16),
            [topTex] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var minY = float.MaxValue;
        var maxY = float.MinValue;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var i = 0; i < verts.Length / stride; i++)
        {
            minY = MathF.Min(minY, verts[i * stride + 1]);
            maxY = MathF.Max(maxY, verts[i * stride + 1]);
        }

        Assert.InRange(minY, -0.51f, -0.49f);
        Assert.InRange(maxY, 1.49f, 1.51f);
    }

    private static Vector2 MeasureUvSpan(float[] verts)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var minU = float.MaxValue;
        var maxU = float.MinValue;
        var minV = float.MaxValue;
        var maxV = float.MinValue;
        for (var i = 0; i < verts.Length / stride; i++)
        {
            minU = MathF.Min(minU, verts[i * stride + 6]);
            maxU = MathF.Max(maxU, verts[i * stride + 6]);
            minV = MathF.Min(minV, verts[i * stride + 7]);
            maxV = MathF.Max(maxV, verts[i * stride + 7]);
        }

        return new Vector2(maxU - minU, maxV - minV);
    }

    private static List<Vector2> CollectFaceUvs(float[] verts, Vector3 faceNormal)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var tolerance = 0.01f;
        var uvs = new List<Vector2>();
        for (var i = 0; i < verts.Length / stride; i++)
        {
            var nx = verts[i * stride + 3];
            var ny = verts[i * stride + 4];
            var nz = verts[i * stride + 5];
            if (MathF.Abs(nx - faceNormal.X) < tolerance &&
                MathF.Abs(ny - faceNormal.Y) < tolerance &&
                MathF.Abs(nz - faceNormal.Z) < tolerance)
            {
                uvs.Add(new Vector2(verts[i * stride + 6], verts[i * stride + 7]));
            }
        }

        return uvs;
    }
}
