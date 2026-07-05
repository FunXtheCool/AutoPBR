using System.Numerics;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// OpenGL / GLES clip-space matrices. <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/> follows Direct3D-style
/// conventions and can clip the entire preview frustum on ANGLE; use these helpers instead.
/// </summary>
internal static class PreviewGlMatrices
{
    /// <summary>
    /// Right-handed perspective matching classic OpenGL NDC (z in [-1,1]).
    /// Stored in <see cref="Matrix4x4"/> row-major order; the preview renderer transposes before
    /// <c>glUniformMatrix4fv</c> so GLSL receives the usual column-major matrix (clip z column
    /// <c>(0,0,a,-1)</c>, clip w column <c>(0,0,b,0)</c>).
    /// </summary>
    public static Matrix4x4 CreatePerspectiveFieldOfViewOpenGl(float fieldOfViewYRadians, float aspectRatio,
        float zNear, float zFar)
    {
        if (aspectRatio <= 0 || zNear <= 0 || zFar <= zNear)
        {
            return Matrix4x4.Identity;
        }

        var h = 1f / MathF.Tan(fieldOfViewYRadians * 0.5f);
        var w = h / aspectRatio;
        var a = (zFar + zNear) / (zNear - zFar);
        var b = (2f * zFar * zNear) / (zNear - zFar);
        return new Matrix4x4(
            w, 0, 0, 0,
            0, h, 0, 0,
            0, 0, a, b,
            0, 0, -1, 0);
    }

    public static Matrix4x4 ApplyProjectionJitter(Matrix4x4 projection, Vector2 ndcJitter)
    {
        projection.M13 += ndcJitter.X * projection.M43;
        projection.M23 += ndcJitter.Y * projection.M43;
        return projection;
    }

    /// <summary>
    /// Right-handed world→view matrix (glm <c>lookAt</c> RH equivalent). Stored in row-major
    /// <see cref="Matrix4x4"/> layout matching <see cref="CreatePerspectiveFieldOfViewOpenGl"/>:
    /// transpose once before <c>glUniformMatrix4fv</c>.
    /// Using this instead of <see cref="Matrix4x4.CreateLookAt"/> keeps handedness consistent with the custom
    /// projection so orbit (eye moving on a sphere around the pivot) reads correctly in depth and parallax.
    /// </summary>
    public static Matrix4x4 CreateLookAtRhOpenGlRowStorage(Vector3 eye, Vector3 target, Vector3 up)
    {
        var f = Vector3.Normalize(target - eye);
        var s = Vector3.Normalize(Vector3.Cross(f, up));
        if (s.LengthSquared() < 1e-12f)
        {
            s = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitZ));
        }

        var u = Vector3.Cross(s, f);

        return new Matrix4x4(
            s.X, s.Y, s.Z, -Vector3.Dot(s, eye),
            u.X, u.Y, u.Z, -Vector3.Dot(u, eye),
            -f.X, -f.Y, -f.Z, Vector3.Dot(f, eye),
            0f, 0f, 0f, 1f);
    }

    /// <summary>
    /// Right-handed orthographic projection matching OpenGL NDC (z in [-1,1]). Stored in row-major
    /// <see cref="Matrix4x4"/> layout matching <see cref="CreatePerspectiveFieldOfViewOpenGl"/>:
    /// transpose once before <c>glUniformMatrix4fv</c>. Used by the directional shadow map pass
    /// so the light frustum keeps consistent handedness with the main camera.
    /// </summary>
    public static Matrix4x4 CreateOrthographicOpenGlRowStorage(float left, float right, float bottom, float top,
        float zNear, float zFar)
    {
        var dx = right - left;
        var dy = top - bottom;
        var dz = zFar - zNear;
        if (dx <= 0 || dy <= 0 || dz <= 0)
        {
            return Matrix4x4.Identity;
        }

        var sx = 2f / dx;
        var sy = 2f / dy;
        var sz = -2f / dz;
        var tx = -(right + left) / dx;
        var ty = -(top + bottom) / dy;
        var tz = -(zFar + zNear) / dz;
        return new Matrix4x4(
            sx, 0, 0, tx,
            0, sy, 0, ty,
            0, 0, sz, tz,
            0, 0, 0, 1f);
    }
}
