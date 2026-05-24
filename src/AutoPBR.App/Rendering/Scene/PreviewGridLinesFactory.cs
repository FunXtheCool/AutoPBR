using System.Numerics;

namespace AutoPBR.App.Rendering.Scene;

/// <summary>Interleaved line vertices: vec3 position, vec4 rgba (for unlit line shader).</summary>
public static class PreviewGridLinesFactory
{
    public const int FloatsPerVertex = 7;

    /// <summary>XZ grid on a horizontal plane (world Y = <paramref name="y"/>).</summary>
    public static float[] BuildGrid(float halfExtent, float step, float y, float cr, float cg, float cb, float ca)
    {
        var list = new List<float>(512);
        void Vertex(Vector3 p)
        {
            list.Add(p.X);
            list.Add(p.Y);
            list.Add(p.Z);
            list.Add(cr);
            list.Add(cg);
            list.Add(cb);
            list.Add(ca);
        }

        void AddLine(Vector3 p0, Vector3 p1)
        {
            Vertex(p0);
            Vertex(p1);
        }

        for (var z = -halfExtent; z <= halfExtent + 1e-4f; z += step)
        {
            AddLine(new Vector3(-halfExtent, y, z), new Vector3(halfExtent, y, z));
        }

        for (var x = -halfExtent; x <= halfExtent + 1e-4f; x += step)
        {
            AddLine(new Vector3(x, y, -halfExtent), new Vector3(x, y, halfExtent));
        }

        return list.ToArray();
    }

    /// <summary>Three axis segments from origin along +X,+Y,+Z (model space).</summary>
    public static float[] BuildAxes(float halfLen, float rX, float gX, float bX, float rY, float gY, float bY,
        float rZ, float gZ, float bZ)
    {
        float[] Seg(float x0, float y0, float z0, float x1, float y1, float z1, float r, float g, float b) =>
            [
                x0, y0, z0, r, g, b, 1f,
                x1, y1, z1, r, g, b, 1f
            ];

        var o = new List<float>(42);
        o.AddRange(Seg(0, 0, 0, halfLen, 0, 0, rX, gX, bX));
        o.AddRange(Seg(0, 0, 0, 0, halfLen, 0, rY, gY, bY));
        o.AddRange(Seg(0, 0, 0, 0, 0, halfLen, rZ, gZ, bZ));
        return o.ToArray();
    }
}
