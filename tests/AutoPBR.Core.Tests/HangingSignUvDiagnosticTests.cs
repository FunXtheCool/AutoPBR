using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class HangingSignUvDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string Path = "assets/minecraft/textures/entity/signs/hanging/acacia.png";

    [Fact]
    public void Ceiling_chains_use_north_south_uvSpan_regions()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var chains = mesh.Elements
            .Where(el => el.Faces.ContainsKey("north") &&
                         el.Faces.ContainsKey("south") &&
                         !el.Faces.ContainsKey("east") &&
                         MathF.Abs(el.To[1] - el.From[1] - 6f) < 0.15f &&
                         MathF.Abs(el.To[0] - el.From[0] - 3f) < 0.15f)
            .ToList();
        Assert.Equal(4, chains.Count);

        foreach (var chain in chains.Where(el => MathF.Abs(el.Faces["north"].Uv![0] - 3f) < 0.01f))
        {
            Assert.Equal(new float[] { 3, 12, 6, 6 }, chain.Faces["north"].Uv!);
            Assert.Equal(new float[] { 0, 12, 3, 6 }, chain.Faces["south"].Uv!);
        }

        foreach (var chain in chains.Where(el => MathF.Abs(el.Faces["north"].Uv![0] - 9f) < 0.01f))
        {
            Assert.Equal(new float[] { 9, 12, 12, 6 }, chain.Faces["north"].Uv!);
            Assert.Equal(new float[] { 6, 12, 9, 6 }, chain.Faces["south"].Uv!);
        }
    }

    [Fact]
    public void Ceiling_tilted_chain_baked_uv_samples_link_texOffs_region()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);
        AssertChainBakedUvHits(mesh, uMin: 0f, uMax: 4f / 64f, vMin: 6f / 32f, vMax: 13f / 32f, minHits: 8);
    }

    [Fact]
    public void Ceiling_middle_vertical_chain_baked_uv_samples_vChains_region()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.CeilingMiddle);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);
        AssertChainBakedUvHits(mesh, uMin: 14f / 64f, uMax: 39f / 64f, vMin: 6f / 32f, vMax: 13f / 32f, minHits: 4);
    }

    [Fact]
    public void Ceiling_middle_vertical_chain_baked_uv_increases_with_world_y()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.CeilingMiddle);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        const float uMin = 14f / 64f;
        const float uMax = 39f / 64f;
        const float vMin = 6f / 32f;
        const float vMax = 13f / 32f;
        var chainVerts = new List<(float y, float v)>();
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (u >= uMin && u <= uMax && v >= vMin && v <= vMax)
            {
                chainVerts.Add((verts[i + 1], v));
            }
        }

        Assert.True(chainVerts.Count >= 4, $"expected chain verts in vChains region, got {chainVerts.Count}");
        var minY = chainVerts.Min(p => p.y);
        var maxY = chainVerts.Max(p => p.y);
        var bottomV = chainVerts.Where(p => MathF.Abs(p.y - minY) < 0.02f).Average(p => p.v);
        var topV = chainVerts.Where(p => MathF.Abs(p.y - maxY) < 0.02f).Average(p => p.v);
        Assert.True(
            topV < bottomV - 0.01f,
            $"expected chain UV to decrease toward ceiling (topV={topV:F4} bottomV={bottomV:F4})");
    }

    [Fact]
    public void Wall_tilted_chain_baked_uv_samples_link_texOffs_region()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.Wall);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);
        AssertChainBakedUvHits(mesh, uMin: 0f, uMax: 4f / 64f, vMin: 6f / 32f, vMax: 13f / 32f, minHits: 8);
    }

    private static void AssertChainBakedUvHits(
        MergedJavaBlockModel mesh,
        float uMin,
        float uMax,
        float vMin,
        float vMax,
        int minHits)
    {
        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var hits = 0;
        for (var i = 0; i < verts.Length; i += stride)
        {
            var u = verts[i + 6];
            var v = verts[i + 7];
            if (u >= uMin && u <= uMax && v >= vMin && v <= vMax)
            {
                hits++;
            }
        }

        Assert.True(hits >= minHits, $"expected >= {minHits} baked verts in UV rect, got {hits}");
    }

    [Fact]
    public void Ceiling_middle_chain_uses_vertical_uvSpan_region()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.CeilingMiddle);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var chain = mesh.Elements.Single(el =>
            el.Faces.ContainsKey("north") &&
            el.Faces.ContainsKey("south") &&
            !el.Faces.ContainsKey("east") &&
            MathF.Abs(el.To[0] - el.From[0] - 12f) < 0.15f);
        Assert.Equal(new float[] { 26, 12, 38, 6 }, chain.Faces["north"].Uv!);
        Assert.Equal(new float[] { 14, 12, 26, 6 }, chain.Faces["south"].Uv!);
    }

    [Fact]
    public void Wall_plank_uses_full_box_uv_origin()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using var scope = EntityPreviewBuildContext.UseContextType(EntityPreviewContextTypeCatalog.Wall);
        Assert.True(runtime.TryBuildStaticMesh(Path, Profile26, 0f, 0f, out var mesh), Path);

        var plank = mesh.Elements.Single(el =>
            MathF.Abs(el.To[0] - el.From[0] - 16f) < 0.15f &&
            MathF.Abs(el.To[1] - el.From[1] - 2f) < 0.15f);
        Assert.Equal(new float[] { 4, 4, 20, 6 }, plank.Faces["north"].Uv!);
        Assert.Equal(new float[] { 24, 4, 40, 6 }, plank.Faces["south"].Uv!);
        Assert.Equal(new float[] { 4, 0, 20, 4 }, plank.Faces["up"].Uv!);
    }
}
