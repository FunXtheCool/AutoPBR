using System.Numerics;

namespace AutoPBR.App.Rendering.Scene;

internal static class PreviewTangentBasis
{
    public static void Derive(
        ReadOnlySpan<Vector3> corners,
        ReadOnlySpan<Vector2> uvs,
        Vector3 normal,
        Vector3 fallbackTangent,
        float fallbackWSign,
        out Vector3 tangent,
        out float wSign)
    {
        if (corners.Length >= 4 && uvs.Length >= 4 &&
            (TryDerive(corners[0], corners[1], corners[2], uvs[0], uvs[1], uvs[2], normal, out tangent, out wSign) ||
             TryDerive(corners[0], corners[2], corners[3], uvs[0], uvs[2], uvs[3], normal, out tangent, out wSign)))
        {
            return;
        }

        tangent = OrthogonalizeOrFallback(fallbackTangent, normal);
        wSign = fallbackWSign;
    }

    private static bool TryDerive(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector3 normal,
        out Vector3 tangent,
        out float wSign)
    {
        tangent = default;
        wSign = 1f;

        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var duv1 = uv1 - uv0;
        var duv2 = uv2 - uv0;
        var det = duv1.X * duv2.Y - duv1.Y * duv2.X;
        if (MathF.Abs(det) < 1e-8f)
        {
            return false;
        }

        var invDet = 1f / det;
        var rawTangent = (edge1 * duv2.Y - edge2 * duv1.Y) * invDet;
        var rawBitangent = (edge2 * duv1.X - edge1 * duv2.X) * invDet;
        tangent = rawTangent - normal * Vector3.Dot(normal, rawTangent);
        if (!TryNormalize(tangent, out tangent) || !TryNormalize(rawBitangent, out var bitangent))
        {
            return false;
        }

        wSign = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) < 0f ? -1f : 1f;
        return true;
    }

    private static Vector3 OrthogonalizeOrFallback(Vector3 tangent, Vector3 normal)
    {
        var ortho = tangent - normal * Vector3.Dot(normal, tangent);
        if (TryNormalize(ortho, out ortho))
        {
            return ortho;
        }

        var axis = MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        ortho = Vector3.Cross(axis, normal);
        return TryNormalize(ortho, out ortho) ? ortho : Vector3.UnitX;
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        var lenSq = value.LengthSquared();
        if (lenSq < 1e-12f || float.IsNaN(lenSq) || float.IsInfinity(lenSq))
        {
            normalized = default;
            return false;
        }

        normalized = value / MathF.Sqrt(lenSq);
        return true;
    }
}
