using System.Numerics;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Screen-space sun disc projection shared by the sun billboard, atmospheric sky bloom, and god rays.
/// </summary>
internal static class PreviewSunScreenProjection
{
    public const float SunDistance = 85f;
    public const float SunRadius = 5.5f;
    /// <summary>Moon angular diameter ~48% of the sun (real ~0.52° vs ~0.53°).</summary>
    public const float MoonRadius = 2.65f;
    public const float DiscUvMargin = 1.2f;
    public const float ShaftScale = 14f;
    public const float MinShaftRadiusUv = 0.11f;

    /// <summary>
    /// Projects the sun billboard center and disc/cone radii in normalized viewport UV (origin bottom-left).
    /// </summary>
    /// <param name="eye">Camera position in world space.</param>
    /// <param name="lightPropagationDir">Normalized light travel direction (away from the sun).</param>
    /// <param name="view">View matrix.</param>
    /// <param name="proj">Projection matrix.</param>
    /// <param name="viewportAspect">Viewport width divided by height.</param>
    /// <param name="coneScale">User shaft-width multiplier (see <c>GodRayConeScale</c>).</param>
    /// <param name="sunSizeScale">User sun angular-size multiplier (1 = legacy size; ~0.07 = real sun).</param>
    /// <param name="sunUv">Sun center in normalized viewport UV.</param>
    /// <param name="sunDiscRadiusUv">Sun disc radius in normalized viewport UV.</param>
    /// <param name="sunConeRadiusUv">God-ray cone radius in normalized viewport UV.</param>
    /// <param name="sunCosDiscEdge">
    /// Cosine of the angular radius from the camera to the sun disc edge (matches the sun billboard bearing).
    /// </param>
    public static void Compute(
        Vector3 eye,
        Vector3 lightPropagationDir,
        Matrix4x4 view,
        Matrix4x4 proj,
        float viewportAspect,
        float coneScale,
        float sunSizeScale,
        out Vector2 sunUv,
        out float sunDiscRadiusUv,
        out float sunConeRadiusUv,
        out float sunCosDiscEdge)
    {
        coneScale = Math.Max(coneScale, 0.05f);
        var sunRadius = SunRadius * Math.Clamp(sunSizeScale, 0.05f, 2f);
        var towardSun = -lightPropagationDir;
        var tls = towardSun.LengthSquared();
        if (tls < 1e-12f)
        {
            sunUv = new Vector2(0.5f, 0.5f);
            sunDiscRadiusUv = 0.025f;
            sunConeRadiusUv = MinShaftRadiusUv;
            sunCosDiscEdge = 0.999f;
            return;
        }

        towardSun /= MathF.Sqrt(tls);

        var worldUp = Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(worldUp, towardSun));
        if (right.LengthSquared() < 1e-10f)
        {
            right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, towardSun));
        }

        var sunCenter = eye + towardSun * SunDistance;
        var sunEdge = sunCenter + right * sunRadius;
        var viewProj = proj * view;

        sunUv = WorldToViewportUv(sunCenter, viewProj);
        var edgeUv = WorldToViewportUv(sunEdge, viewProj);
        var discRadius = Vector2.Distance(
            sunUv with { X = sunUv.X * viewportAspect },
            edgeUv with { X = edgeUv.X * viewportAspect }) * DiscUvMargin;
        sunDiscRadiusUv = Math.Max(discRadius, 0.008f);
        sunConeRadiusUv = Math.Max(sunDiscRadiusUv * ShaftScale * coneScale, MinShaftRadiusUv * coneScale);

        var edgeDir = sunEdge - eye;
        var edgeLen2 = edgeDir.LengthSquared();
        sunCosDiscEdge = edgeLen2 < 1e-12f
            ? 0.999f
            : Math.Clamp(Vector3.Dot(towardSun, edgeDir / MathF.Sqrt(edgeLen2)), 0.85f, 0.999999f);
    }

    /// <summary>Projects the antipodal moon disc (opposite the sun light propagation direction).</summary>
    public static void ComputeMoon(
        Vector3 eye,
        Vector3 lightPropagationDir,
        Matrix4x4 view,
        Matrix4x4 proj,
        float viewportAspect,
        out Vector2 moonUv,
        out float moonDiscRadiusUv,
        out float moonCosDiscEdge)
    {
        var towardMoon = lightPropagationDir;
        var tlm = towardMoon.LengthSquared();
        if (tlm < 1e-12f)
        {
            moonUv = new Vector2(0.5f, 0.5f);
            moonDiscRadiusUv = 0.012f;
            moonCosDiscEdge = 0.9995f;
            return;
        }

        towardMoon /= MathF.Sqrt(tlm);

        var worldUp = Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(worldUp, towardMoon));
        if (right.LengthSquared() < 1e-10f)
        {
            right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, towardMoon));
        }

        var moonCenter = eye + towardMoon * SunDistance;
        var moonEdge = moonCenter + right * MoonRadius;
        var viewProj = proj * view;

        moonUv = WorldToViewportUv(moonCenter, viewProj);
        var edgeUv = WorldToViewportUv(moonEdge, viewProj);
        var discRadius = Vector2.Distance(
            moonUv with { X = moonUv.X * viewportAspect },
            edgeUv with { X = edgeUv.X * viewportAspect }) * DiscUvMargin;
        moonDiscRadiusUv = Math.Max(discRadius, 0.004f);

        var edgeDir = moonEdge - eye;
        var edgeLen2 = edgeDir.LengthSquared();
        moonCosDiscEdge = edgeLen2 < 1e-12f
            ? 0.9995f
            : Math.Clamp(Vector3.Dot(towardMoon, edgeDir / MathF.Sqrt(edgeLen2)), 0.92f, 0.99998f);
    }

    public static Vector2 WorldToViewportUv(Vector3 worldPos, Matrix4x4 viewProjRow)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProjRow);
        if (clip.W <= 1e-6f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        var invW = 1f / clip.W;
        var ndc = new Vector2(clip.X * invW, clip.Y * invW);
        return new Vector2(ndc.X * 0.5f + 0.5f, ndc.Y * 0.5f + 0.5f);
    }
}
