using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

/// <summary>
/// Extrudes each opaque sprite texel into an axis-aligned cuboid (Minecraft-style item thickening).
/// Every face samples the same texel center so side caps show flat pixel color, not stretched UV.
/// </summary>
internal static class SpriteVoxelMeshBuilder
{
    public static PreviewMesh Build(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        float thickness,
        float alphaCutoff = 0.5f,
        string name = "sprite_voxels")
    {
        if (width <= 0 || height <= 0 || thickness <= 1e-6f || rgba.Length < width * height * 4)
        {
            return PreviewMeshFactory.CreateItemPlane(name: name);
        }

        var voxelSize = 1f / Math.Max(width, height);
        var depthScale = thickness / (float)PreviewStageConstants.SpriteThicknessMax;
        var depth = depthScale * voxelSize;
        if (depth <= 1e-6f)
        {
            return PreviewMeshFactory.CreateItemPlane(name: name);
        }

        var z0 = -depth * 0.5f;
        var z1 = depth * 0.5f;
        var originX = -(width * voxelSize) * 0.5f;
        var originY = -(height * voxelSize) * 0.5f;

        var verts = new List<float>(256 * PreviewMesh.FloatsPerVertex);
        var indices = new List<uint>(256 * 6);

        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var i = (py * width + px) * 4;
                if (rgba[i + 3] / 255f < alphaCutoff)
                {
                    continue;
                }

                var x0 = originX + px * voxelSize;
                var x1 = x0 + voxelSize;
                var y1 = originY + (height - py) * voxelSize;
                var y0 = y1 - voxelSize;
                var u = (px + 0.5f) / width;
                var v = (py + 0.5f) / height;
                var uv = new Vector2(u, v);

                AddCuboid(x0, y0, z0, x1, y1, z1, uv, verts, indices);
            }
        }

        if (verts.Count == 0)
        {
            return PreviewMeshFactory.CreateItemPlane(name: name);
        }

        return new PreviewMesh
        {
            Name = name,
            InterleavedVertices = verts.ToArray(),
            Indices = indices.ToArray()
        };
    }

    private static void AddCuboid(
        float x0,
        float y0,
        float z0,
        float x1,
        float y1,
        float z1,
        Vector2 uv,
        List<float> verts,
        List<uint> indices)
    {
        AddSolidFace(new Vector3(0, 0, 1), new Vector3(1, 0, 0), 1f,
            [new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1)], uv, verts, indices);
        AddSolidFace(new Vector3(0, 0, -1), new Vector3(-1, 0, 0), 1f,
            [new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0)], uv, verts, indices);
        AddSolidFace(new Vector3(1, 0, 0), new Vector3(0, 0, -1), 1f,
            [new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1)], uv, verts, indices);
        AddSolidFace(new Vector3(-1, 0, 0), new Vector3(0, 0, 1), 1f,
            [new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0)], uv, verts, indices);
        AddSolidFace(new Vector3(0, 1, 0), new Vector3(1, 0, 0), 1f,
            [new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0)], uv, verts, indices);
        AddSolidFace(new Vector3(0, -1, 0), new Vector3(1, 0, 0), -1f,
            [new(x0, y0, z1), new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1)], uv, verts, indices);
    }

    private static void AddSolidFace(
        Vector3 normal,
        Vector3 tangent,
        float wSign,
        ReadOnlySpan<Vector3> corners,
        Vector2 uv,
        List<float> verts,
        List<uint> indices)
    {
        var baseIndex = (uint)(verts.Count / PreviewMesh.FloatsPerVertex);
        for (var i = 0; i < 4; i++)
        {
            var p = corners[i];
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

        indices.Add(baseIndex);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }
}
