using System.Numerics;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class PreviewFrustumPlanes
{
    public const int PlaneCount = 6;

    public static void Extract(Matrix4x4 viewProjection, Span<Vector4> destination)
    {
        if (destination.Length < PlaneCount)
        {
            throw new ArgumentException("Frustum destination must hold six planes.", nameof(destination));
        }

        var r1 = new Vector4(viewProjection.M11, viewProjection.M12, viewProjection.M13, viewProjection.M14);
        var r2 = new Vector4(viewProjection.M21, viewProjection.M22, viewProjection.M23, viewProjection.M24);
        var r3 = new Vector4(viewProjection.M31, viewProjection.M32, viewProjection.M33, viewProjection.M34);
        var r4 = new Vector4(viewProjection.M41, viewProjection.M42, viewProjection.M43, viewProjection.M44);

        destination[0] = Normalize(r4 + r1); // left
        destination[1] = Normalize(r4 - r1); // right
        destination[2] = Normalize(r4 + r2); // bottom
        destination[3] = Normalize(r4 - r2); // top
        destination[4] = Normalize(r4 + r3); // near
        destination[5] = Normalize(r4 - r3); // far
    }

    private static Vector4 Normalize(Vector4 plane)
    {
        var length = new Vector3(plane.X, plane.Y, plane.Z).Length();
        return length > 1e-7f && float.IsFinite(length) ? plane / length : Vector4.Zero;
    }
}
