using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

/// <summary>
/// Extrudes each opaque sprite texel into an axis-aligned cuboid (Minecraft-style item thickening).
/// Coplanar exterior side faces are greedy-merged; shared faces between neighbors are culled.
/// Front/back caps stay one quad per texel so pixel colors stay sharp.
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

        var solid = BuildSolidMask(rgba, width, height, alphaCutoff, out var voxelCount);
        if (voxelCount == 0)
        {
            return PreviewMeshFactory.CreateItemPlane(name: name);
        }

        var z0 = -depth * 0.5f;
        var z1 = depth * 0.5f;
        var originX = -(width * voxelSize) * 0.5f;
        var originY = -(height * voxelSize) * 0.5f;

        var verts = new List<float>(voxelCount * 16 * PreviewMesh.FloatsPerVertex);
        var indices = new List<uint>(voxelCount * 24);

        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                if (!solid[py * width + px])
                {
                    continue;
                }

                var x0 = originX + px * voxelSize;
                var x1 = x0 + voxelSize;
                var y1 = originY + (height - py) * voxelSize;
                var y0 = y1 - voxelSize;
                var uv = TexelCenterUv(px, py, width, height);

                AddSolidFace(new Vector3(0, 0, 1), new Vector3(1, 0, 0), 1f,
                    [new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1)],
                    [uv, uv, uv, uv], verts, indices);
                AddSolidFace(new Vector3(0, 0, -1), new Vector3(-1, 0, 0), 1f,
                    [new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0)],
                    [uv, uv, uv, uv], verts, indices);
            }
        }

        EmitMergedAxisFaces(
            solid, width, height, originX, originY, voxelSize, z0, z1, axis: +1, verts, indices);
        EmitMergedAxisFaces(
            solid, width, height, originX, originY, voxelSize, z0, z1, axis: -1, verts, indices);
        EmitMergedAxisFaces(
            solid, width, height, originX, originY, voxelSize, z0, z1, axis: +2, verts, indices);
        EmitMergedAxisFaces(
            solid, width, height, originX, originY, voxelSize, z0, z1, axis: -2, verts, indices);

        return new PreviewMesh
        {
            Name = name,
            InterleavedVertices = verts.ToArray(),
            Indices = indices.ToArray(),
            OpaqueVoxelCount = voxelCount
        };
    }

    /// <summary>Axis: +1/-1 = ±X, +2/-2 = ±Y.</summary>
    private static void EmitMergedAxisFaces(
        bool[] solid,
        int width,
        int height,
        float originX,
        float originY,
        float voxelSize,
        float z0,
        float z1,
        int axis,
        List<float> verts,
        List<uint> indices)
    {
        if (axis is +1 or -1)
        {
            for (var px = 0; px < width; px++)
            {
                var py = 0;
                while (py < height)
                {
                    if (!IsExposed(solid, width, height, px, py, axis))
                    {
                        py++;
                        continue;
                    }

                    var pyStart = py;
                    while (py < height && IsExposed(solid, width, height, px, py, axis))
                    {
                        py++;
                    }

                    var pyEnd = py - 1;
                    var xPlane = axis > 0
                        ? originX + (px + 1) * voxelSize
                        : originX + px * voxelSize;
                    var yBottom = originY + (height - pyEnd - 1) * voxelSize;
                    var yTop = originY + (height - pyStart) * voxelSize;
                    var uvBottom = TexelCenterUv(px, pyEnd, width, height);
                    var uvTop = TexelCenterUv(px, pyStart, width, height);

                    if (axis > 0)
                    {
                        AddSolidFace(new Vector3(1, 0, 0), new Vector3(0, 0, -1), 1f,
                            [new(xPlane, yBottom, z1), new(xPlane, yBottom, z0), new(xPlane, yTop, z0), new(xPlane, yTop, z1)],
                            [uvBottom, uvBottom, uvTop, uvTop], verts, indices);
                    }
                    else
                    {
                        AddSolidFace(new Vector3(-1, 0, 0), new Vector3(0, 0, 1), 1f,
                            [new(xPlane, yBottom, z0), new(xPlane, yBottom, z1), new(xPlane, yTop, z1), new(xPlane, yTop, z0)],
                            [uvBottom, uvBottom, uvTop, uvTop], verts, indices);
                    }
                }
            }

            return;
        }

        for (var py = 0; py < height; py++)
        {
            var px = 0;
            while (px < width)
            {
                if (!IsExposed(solid, width, height, px, py, axis))
                {
                    px++;
                    continue;
                }

                var pxStart = px;
                while (px < width && IsExposed(solid, width, height, px, py, axis))
                {
                    px++;
                }

                var pxEnd = px - 1;
                var yPlane = axis > 0
                    ? originY + (height - py) * voxelSize
                    : originY + (height - py - 1) * voxelSize;
                var xLeft = originX + pxStart * voxelSize;
                var xRight = originX + (pxEnd + 1) * voxelSize;
                var uvLeft = TexelCenterUv(pxStart, py, width, height);
                var uvRight = TexelCenterUv(pxEnd, py, width, height);

                if (axis > 0)
                {
                    AddSolidFace(new Vector3(0, 1, 0), new Vector3(1, 0, 0), 1f,
                        [new(xLeft, yPlane, z0), new(xLeft, yPlane, z1), new(xRight, yPlane, z1), new(xRight, yPlane, z0)],
                        [uvLeft, uvLeft, uvRight, uvRight], verts, indices);
                }
                else
                {
                    AddSolidFace(new Vector3(0, -1, 0), new Vector3(1, 0, 0), -1f,
                        [new(xLeft, yPlane, z1), new(xLeft, yPlane, z0), new(xRight, yPlane, z0), new(xRight, yPlane, z1)],
                        [uvLeft, uvLeft, uvRight, uvRight], verts, indices);
                }
            }
        }
    }

    private static bool IsExposed(bool[] solid, int width, int height, int px, int py, int axis) =>
        axis switch
        {
            +1 => solid[py * width + px] && (px + 1 >= width || !solid[py * width + px + 1]),
            -1 => solid[py * width + px] && (px <= 0 || !solid[py * width + px - 1]),
            +2 => solid[py * width + px] && (py <= 0 || !solid[(py - 1) * width + px]),
            -2 => solid[py * width + px] && (py + 1 >= height || !solid[(py + 1) * width + px]),
            _ => false
        };

    private static bool[] BuildSolidMask(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        float alphaCutoff,
        out int voxelCount)
    {
        var solid = new bool[width * height];
        voxelCount = 0;
        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var i = (py * width + px) * 4;
                if (rgba[i + 3] / 255f >= alphaCutoff)
                {
                    solid[py * width + px] = true;
                    voxelCount++;
                }
            }
        }

        return solid;
    }

    private static Vector2 TexelCenterUv(int px, int py, int width, int height) =>
        new((px + 0.5f) / width, 1f - (py + 0.5f) / height);

    private static void AddSolidFace(
        Vector3 normal,
        Vector3 fallbackTangent,
        float fallbackWSign,
        ReadOnlySpan<Vector3> corners,
        ReadOnlySpan<Vector2> uvs,
        List<float> verts,
        List<uint> indices)
    {
        PreviewTangentBasis.Derive(corners, uvs, normal, fallbackTangent, fallbackWSign, out var tangent, out var wSign);
        var baseIndex = (uint)(verts.Count / PreviewMesh.FloatsPerVertex);
        for (var i = 0; i < 4; i++)
        {
            var p = corners[i];
            var uv = uvs[i];
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
