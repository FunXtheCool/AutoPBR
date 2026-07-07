using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class PreviewMeshFactory
{
    /// <summary>Zero indices so the Genesis mesh draw is skipped while keeping a valid VAO layout.</summary>
    public static PreviewMesh CreateEmptySubjectPlaceholder(string name = "emptySubject") =>
        new()
        {
            Name = name,
            InterleavedVertices = [],
            Indices = []
        };

    public static PreviewMesh CreateUnitCube(string name = "cube")
    {
        var v = new List<float>(24 * PreviewMesh.FloatsPerVertex);
        var idx = new List<uint>(36);

        void AddQuad(Vector3 n, Vector3 fallbackTangent, float fallbackWSign, ReadOnlySpan<Vector3> corners,
            ReadOnlySpan<Vector2> uvs)
        {
            PreviewTangentBasis.Derive(corners, uvs, n, fallbackTangent, fallbackWSign, out var t, out var wSign);
            var baseIndex = (uint)(v.Count / PreviewMesh.FloatsPerVertex);
            for (var i = 0; i < 4; i++)
            {
                var p = corners[i];
                var uv = uvs[i];
                v.Add(p.X);
                v.Add(p.Y);
                v.Add(p.Z);
                v.Add(n.X);
                v.Add(n.Y);
                v.Add(n.Z);
                v.Add(uv.X);
                v.Add(uv.Y);
                v.Add(t.X);
                v.Add(t.Y);
                v.Add(t.Z);
                v.Add(wSign);
            }

            idx.Add(baseIndex);
            idx.Add(baseIndex + 1);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex + 3);
        }

        const float h = 0.5f;

        // +Z
        AddQuad(new Vector3(0, 0, 1), new Vector3(1, 0, 0), 1f,
            new[]
            {
                new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h)
            },
            new Vector2[] { new(0, 1), new(1, 1), new(1, 0), new(0, 0) });
        // -Z
        AddQuad(new Vector3(0, 0, -1), new Vector3(-1, 0, 0), 1f,
            new[] { new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h) },
            new Vector2[] { new(0, 1), new(1, 1), new(1, 0), new(0, 0) });
        // +X — vertex order must wind CCW when viewed from +X so GL_BACK cull keeps outward-facing tris (±Z/±Y already matched this).
        AddQuad(new Vector3(1, 0, 0), new Vector3(0, 0, -1), 1f,
            new[] { new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h) },
            new Vector2[] { new(1, 1), new(0, 1), new(0, 0), new(1, 0) });
        // -X
        AddQuad(new Vector3(-1, 0, 0), new Vector3(0, 0, 1), 1f,
            new[] { new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h) },
            new Vector2[] { new(1, 1), new(0, 1), new(0, 0), new(1, 0) });
        // +Y
        AddQuad(new Vector3(0, 1, 0), new Vector3(1, 0, 0), 1f,
            new[] { new Vector3(-h, h, -h), new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h) },
            new Vector2[] { new(0, 1), new(1, 1), new(1, 0), new(0, 0) });
        // -Y
        AddQuad(new Vector3(0, -1, 0), new Vector3(1, 0, 0), -1f,
            new[] { new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h) },
            new Vector2[] { new(0, 1), new(1, 1), new(1, 0), new(0, 0) });

        return new PreviewMesh
        {
            Name = name,
            InterleavedVertices = v.ToArray(),
            Indices = idx.ToArray()
        };
    }

    /// <summary>XY quad centered at origin, facing +Z (viewer from +Z sees front).</summary>
    /// <remarks>
    /// UVs match <see cref="AutoPBR.Core.Preview.PreviewUvBakePolicy.BlockOrItemBaseline"/> with <c>FlipV</c>
    /// plus <see cref="Abstractions.PreviewMaterial.GlUploadFlipRows"/> on upload (same convention as baked block/item faces).
    /// </remarks>
    public static PreviewMesh CreateItemPlane(string name = "item_plane", float halfSize = 0.5f)
    {
        var h = halfSize;
        var n = new Vector3(0, 0, 1);
        Vector3[] corners =
        [
            new(-h, -h, 0),
            new(h, -h, 0),
            new(h, h, 0),
            new(-h, h, 0)
        ];
        Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        PreviewTangentBasis.Derive(corners, uvs, n, Vector3.UnitX, 1f, out var t, out var wSign);
        var verts = new List<float>(4 * PreviewMesh.FloatsPerVertex);
        for (var i = 0; i < 4; i++)
        {
            AddVertex(verts, corners[i], n, uvs[i], t, wSign);
        }

        uint[] flatIndices = [0u, 1u, 2u, 0u, 2u, 3u];
        return new PreviewMesh { Name = name, InterleavedVertices = verts.ToArray(), Indices = flatIndices };
    }

    /// <summary>Per-texel cuboid extrusion for thickened 2D sprite preview.</summary>
    public static PreviewMesh CreateSpritePixelCuboids(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        float thickness,
        float alphaCutoff = 0.5f,
        string name = "sprite_voxels") =>
        SpriteVoxelMeshBuilder.Build(rgba, width, height, thickness, alphaCutoff, name);

    /// <summary>Creates crossed Y-rotated sprite planes centered at origin (Minecraft foliage-like billboard stack).</summary>
    public static PreviewMesh CreateSpritePlanes(string name = "sprite_planes", int planeCount = 2, float halfSize = 0.5f)
    {
        var planes = Math.Clamp(planeCount, 1, 8);
        var v = new List<float>(planes * 4 * PreviewMesh.FloatsPerVertex);
        var idx = new List<uint>(planes * 6);
        var h = halfSize;

        for (var i = 0; i < planes; i++)
        {
            var angle = (MathF.PI * i) / planes;
            var rot = Matrix4x4.CreateRotationY(angle);
            var n = Vector3.TransformNormal(new Vector3(0, 0, 1), rot);
            var t = Vector3.TransformNormal(new Vector3(1, 0, 0), rot);
            var c0 = Vector3.Transform(new Vector3(-h, -h, 0), rot);
            var c1 = Vector3.Transform(new Vector3(h, -h, 0), rot);
            var c2 = Vector3.Transform(new Vector3(h, h, 0), rot);
            var c3 = Vector3.Transform(new Vector3(-h, h, 0), rot);

            Vector3[] corners = [c0, c1, c2, c3];
            Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
            PreviewTangentBasis.Derive(corners, uvs, n, t, 1f, out var tangent, out var wSign);

            var baseIndex = (uint)(v.Count / PreviewMesh.FloatsPerVertex);
            for (var j = 0; j < 4; j++)
            {
                AddVertex(v, corners[j], n, uvs[j], tangent, wSign);
            }

            idx.Add(baseIndex);
            idx.Add(baseIndex + 1);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex + 3);
        }

        return new PreviewMesh { Name = name, InterleavedVertices = v.ToArray(), Indices = idx.ToArray() };
    }

    /// <summary>
    /// XZ ground plane (normal +Y). UVs repeat every <paramref name="metersPerTile"/> world units so a 16×16 block
    /// texture tiles without stretching when filtering is nearest and wrap mode is repeat.
    /// </summary>
    public static PreviewMesh CreatePreviewGroundPlane(string name = "preview_ground",
        float? halfExtent = null,
        float? worldY = null,
        float metersPerTile = PreviewStageConstants.MetersPerGrassTile)
    {
        var h = halfExtent ?? PreviewStageConstants.GridHalfExtent;
        var y = worldY ?? PreviewStageConstants.GroundPlaneWorldY;
        if (metersPerTile <= 1e-6f)
        {
            metersPerTile = PreviewStageConstants.MetersPerGrassTile;
        }

        var n = Vector3.UnitY;
        Vector2 Uv(float x, float z) => new((x + h) / metersPerTile, (z + h) / metersPerTile);

        var verts = new List<float>(4 * PreviewMesh.FloatsPerVertex);

        // CCW when viewed from +Y.
        var p00 = new Vector3(-h, y, -h);
        var p10 = new Vector3(h, y, -h);
        var p11 = new Vector3(h, y, h);
        var p01 = new Vector3(-h, y, h);
        Vector3[] corners = [p00, p10, p11, p01];
        Vector2[] uvs = [Uv(p00.X, p00.Z), Uv(p10.X, p10.Z), Uv(p11.X, p11.Z), Uv(p01.X, p01.Z)];
        PreviewTangentBasis.Derive(corners, uvs, n, Vector3.UnitX, 1f, out var t, out var wSign);
        for (var i = 0; i < 4; i++)
        {
            AddVertex(verts, corners[i], n, uvs[i], t, wSign);
        }

        uint[] indices = [0u, 1u, 2u, 0u, 2u, 3u];
        return new PreviewMesh { Name = name, InterleavedVertices = verts.ToArray(), Indices = indices };
    }

    private static void AddVertex(List<float> verts, Vector3 p, Vector3 normal, Vector2 uv, Vector3 tangent, float wSign)
    {
        verts.Add(p.X);
        verts.Add(p.Y);
        verts.Add(p.Z);
        verts.Add(normal.X);
        verts.Add(normal.Y);
        verts.Add(normal.Z);
        verts.Add(uv.X);
        verts.Add(uv.Y);
        verts.Add(tangent.X);
        verts.Add(tangent.Y);
        verts.Add(tangent.Z);
        verts.Add(wSign);
    }
}
