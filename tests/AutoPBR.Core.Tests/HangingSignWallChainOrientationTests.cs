using System.Numerics;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class HangingSignWallChainOrientationTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string Path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";

    [Theory]
    [InlineData(EntityPreviewContextTypeCatalog.Wall)]
    [InlineData(EntityPreviewContextTypeCatalog.Ceiling)]
    public void Normal_chain_segments_are_mostly_vertical_in_preview_space(string contextType)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(contextType);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var chains = mesh.Elements.Where(IsNormalChainElement).ToList();
        Assert.Equal(4, chains.Count);

        foreach (var chain in chains)
        {
            var dir = ChainAxisDirection(chain);
            var verticality = MathF.Abs(dir.Y) / MathF.Max(MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z), 1e-5f);
            Assert.True(
                verticality >= 0.95f,
                $"chain axis verticality={verticality:F3} dir=({dir.X:F3},{dir.Y:F3},{dir.Z:F3})");
        }
    }

    [Fact]
    public void Wall_chains_span_plank_to_board_in_preview_space()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.Wall);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var plank = mesh.Elements.Single(el => MathF.Abs(el.To[0] - el.From[0] - 16f) < 0.15f);
        var board = mesh.Elements.Single(el =>
            MathF.Abs(el.To[1] - el.From[1] - 10f) < 0.15f &&
            MathF.Abs(el.To[0] - el.From[0] - 14f) < 0.15f);
        TransformWorldCorners(plank, out _, out var plankMax);
        TransformWorldCorners(board, out _, out var boardMax);

        var chainMinY = mesh.Elements.Where(IsNormalChainElement).Min(c =>
        {
            TransformWorldCorners(c, out var min, out _);
            return min.Y;
        });
        var chainMaxY = mesh.Elements.Where(IsNormalChainElement).Max(c =>
        {
            TransformWorldCorners(c, out _, out var max);
            return max.Y;
        });

        Assert.True(
            MathF.Abs(chainMinY - boardMax.Y) <= 0.75f,
            $"chain bottom detached from board: chainMinY={chainMinY:G4} boardMaxY={boardMax.Y:G4}");
        Assert.True(
            MathF.Abs(plankMax.Y - chainMaxY) <= 0.75f,
            $"chain top detached from plank: chainMaxY={chainMaxY:G4} plankMaxY={plankMax.Y:G4}");
    }

    [Fact]
    public void Ceiling_chains_meet_board_top_in_preview_space()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var board = mesh.Elements.Single(el =>
            MathF.Abs(el.To[1] - el.From[1] - 10f) < 0.15f &&
            MathF.Abs(el.To[0] - el.From[0] - 14f) < 0.15f);
        TransformWorldCorners(board, out _, out var boardMax);

        var chainBottomY = mesh.Elements.Where(IsNormalChainElement).Min(c =>
        {
            TransformWorldCorners(c, out var min, out _);
            return min.Y;
        });
        var jointGap = MathF.Abs(boardMax.Y - chainBottomY);
        Assert.True(jointGap <= 0.75f, $"board/chain joint separated by {jointGap:G4} texels");
    }

    [Theory]
    [InlineData(EntityPreviewContextTypeCatalog.Wall)]
    [InlineData(EntityPreviewContextTypeCatalog.Ceiling)]
    public void Normal_chains_swap_north_south_face_slots_after_orientation(string contextType)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(contextType);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        foreach (var chain in mesh.Elements.Where(IsNormalChainElement))
        {
            var northU = chain.Faces["north"].Uv![0];
            if (MathF.Abs(northU - 3f) < 0.01f)
            {
                Assert.Equal(new float[] { 3, 12, 6, 6 }, chain.Faces["north"].Uv!);
                Assert.Equal(new float[] { 0, 12, 3, 6 }, chain.Faces["south"].Uv!);
            }
            else
            {
                Assert.Equal(new float[] { 9, 12, 12, 6 }, chain.Faces["north"].Uv!);
                Assert.Equal(new float[] { 6, 12, 9, 6 }, chain.Faces["south"].Uv!);
            }
        }
    }

    private static bool IsNormalChainElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return el.Faces.ContainsKey("north") &&
               el.Faces.ContainsKey("south") &&
               !el.Faces.ContainsKey("east") &&
               MathF.Abs(width - 3f) < 0.15f &&
               MathF.Abs(height - 6f) < 0.15f;
    }

    private static Vector3 ChainAxisDirection(ModelElement el)
    {
        var top = Vector3.Transform(new Vector3(0f, el.From[1], 0f), el.LocalToParent);
        var bottom = Vector3.Transform(new Vector3(0f, el.To[1], 0f), el.LocalToParent);
        var dir = bottom - top;
        if (dir.LengthSquared() < 1e-6f)
        {
            dir = Vector3.TransformNormal(new Vector3(0f, 1f, 0f), el.LocalToParent);
        }

        return Vector3.Normalize(dir);
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var fx = el.From[0];
        var fy = el.From[1];
        var fz = el.From[2];
        var tx = el.To[0];
        var ty = el.To[1];
        var tz = el.To[2];
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
            (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), el.LocalToParent);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }
}
