using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class BlockTextureParitySurveyTests(ITestOutputHelper? output)
{
    [Fact]
    public void All_synthesizable_catalog_paths_build_synthetic_cube_mesh()
    {
        var paths = BlockTextureParityCatalog.GetSynthesizableCubePreviewPaths();
        if (paths.Count == 0)
        {
            return;
        }

        var failed = new List<string>();
        var shapeCounts = new Dictionary<BlockTextureParityPreviewShape, int>();
        foreach (var path in paths)
        {
            var rule = BlockTextureParityCatalog.ResolveRule(path);
            Assert.NotNull(rule);
            if (!VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out var provenance, out var ordered, out _))
            {
                failed.Add(path);
                continue;
            }

            Assert.Equal(PreviewMeshDriverKind.VanillaBlockParity, provenance.Kind);
            Assert.NotEmpty(merged.Elements);
            Assert.NotEmpty(ordered);
            shapeCounts.TryGetValue(rule!.PreviewShape, out var c);
            shapeCounts[rule.PreviewShape] = c + 1;
        }

        output?.WriteLine($"Synthesizable paths: {paths.Count}");
        foreach (var (shape, count) in shapeCounts.OrderBy(kv => kv.Key.ToString()))
        {
            output?.WriteLine($"  {shape}: {count}");
        }

        output?.WriteLine($"Build failed: {failed.Count}");
        foreach (var p in failed)
        {
            output?.WriteLine($"  FAILED: {p}");
        }

        Assert.Empty(failed);
    }

    [Fact]
    public void Synthesizable_paths_include_complex_shapes()
    {
        var paths = BlockTextureParityCatalog.GetSynthesizableCubePreviewPaths();
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.ThinPlate);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.DoorHalf);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.CakeWedge);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.CactusCross);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.RailTrack);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.CrossSprite);
        Assert.Contains(paths, p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == BlockTextureParityPreviewShape.FenceWithLink);
    }

    [Fact]
    public void Synthesizable_paths_majority_are_cube_shapes()
    {
        var paths = BlockTextureParityCatalog.GetSynthesizableCubePreviewPaths();
        if (paths.Count == 0)
        {
            return;
        }

        var synthesizable = paths.Count(p =>
        {
            var rule = BlockTextureParityCatalog.ResolveRule(p);
            return rule is not null && rule.CanSynthesizeCubePreview();
        });
        Assert.Equal(paths.Count, synthesizable);
        Assert.True(synthesizable >= 900, $"Expected at least 900 synthesizable cube paths, got {synthesizable}");
    }
}
