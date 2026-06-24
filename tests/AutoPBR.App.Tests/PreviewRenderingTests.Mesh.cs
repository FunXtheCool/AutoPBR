using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Fact]
    public void BlockModelPreviewSceneUsesBlockModelKind()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube("subject");
        var scene = BlockModelPreviewSceneFactory.Create(new PreviewRenderSettings(), mesh);
        Assert.Equal(PreviewSceneKind.BlockModel, scene.SceneKind);
        Assert.Same(mesh, scene.Meshes[0]);
    }

    [Fact]
    public void CubeMeshFactoryCreatesSixFacesWithExpectedIndices()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(24 * PreviewMesh.FloatsPerVertex, mesh.InterleavedVertices.Length);
        Assert.Equal(36, mesh.Indices.Length);
    }

    [Fact]
    public void ItemPlaneMeshFactoryCreatesQuadIndices()
    {
        var mesh = PreviewMeshFactory.CreateItemPlane();
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.Indices.Length);
    }

    [Fact]
    public void SpritePixelCuboids_builds_one_cube_per_opaque_texel()
    {
        var rgba = new byte[]
        {
            255, 0, 0, 255, 0, 0, 0, 0,
            0, 0, 0, 0, 255, 0, 0, 255
        };
        var mesh = PreviewMeshFactory.CreateSpritePixelCuboids(rgba, 2, 2, thickness: 0.10f, alphaCutoff: 0.5f);
        Assert.Equal("sprite_voxels", mesh.Name);
        Assert.Equal(48, mesh.VertexCount);
        Assert.Equal(72, mesh.Indices.Length);
    }

    [Fact]
    public void PreviewMaterialMapperCopiesMapsFromCore()
    {
        var diffuse = new byte[4];
        diffuse[0] = 10;
        diffuse[1] = 20;
        diffuse[2] = 30;
        diffuse[3] = 240;
        var maps = new PreviewTextureMaps
        {
            Width = 1,
            Height = 1,
            DiffuseRgba = diffuse,
            NormalRgba = [1, 2, 3, 4],
            SpecularRgba = [5, 6, 7, 8],
            HeightRgba = [9, 10, 11, 12],
            IsPlantForNoHeight = true
        };
        var mat = PreviewMaterialMapper.FromCoreMaps(maps);
        Assert.Equal(1, mat.Width);
        Assert.Equal(1, mat.Height);
        Assert.True(mat.IsPlantForNoHeight);
        Assert.Equal(10, mat.AlbedoRgba.Span[0]);
        Assert.Equal(4, mat.NormalRgba!.Value.Span[3]);
        Assert.Equal(8, mat.SpecularRgba!.Value.Span[3]);
        Assert.Equal(12, mat.HeightRgba!.Value.Span[3]);
    }

    [Fact]
    public void EmptySubjectMeshHasNoIndices()
    {
        var mesh = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
        Assert.Equal(0, mesh.VertexCount);
        Assert.Empty(mesh.Indices);
        Assert.Empty(mesh.InterleavedVertices);
    }

    [Fact]
    public void PreviewGroundPlaneTilingUvSpansOneTilePerWorldUnit()
    {
        const float h = 14f;
        var mesh = PreviewMeshFactory.CreatePreviewGroundPlane(halfExtent: h, worldY: -1f, metersPerTile: 1f);
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.Indices.Length);
        var v = mesh.InterleavedVertices;
        const int s = PreviewMesh.FloatsPerVertex;
        Assert.Equal(0f, v[6]);
        Assert.Equal(0f, v[7]);
        Assert.Equal(2f * h, v[s + 6]);
        Assert.Equal(0f, v[s + 7]);
        Assert.Equal(2f * h, v[2 * s + 6]);
        Assert.Equal(2f * h, v[2 * s + 7]);
        Assert.Equal(0f, v[3 * s + 6]);
        Assert.Equal(2f * h, v[3 * s + 7]);
    }

    [Fact]
    public void GridLinesFactoryVertexBuffersAreSevenFloatsPerVertex()
    {
        var grid = PreviewGridLinesFactory.BuildGrid(1f, 0.5f, -0.5f, 1, 1, 1, 1);
        Assert.Equal(0, grid.Length % PreviewGridLinesFactory.FloatsPerVertex);
        var axes = PreviewGridLinesFactory.BuildAxes(1f, 1, 0, 0, 0, 1, 0, 0, 0, 1);
        Assert.Equal(0, axes.Length % PreviewGridLinesFactory.FloatsPerVertex);
        Assert.Equal(6 * PreviewGridLinesFactory.FloatsPerVertex, axes.Length);
    }
}
