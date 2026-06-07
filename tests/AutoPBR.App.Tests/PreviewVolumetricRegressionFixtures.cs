using System.Numerics;

using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

/// <summary>
/// Fixed camera / light poses for volumetric and sun-projection regression (P0.6).
/// Used by golden projection tests and the manual visual sign-off checklist in
/// <c>docs/volumetric-effects-quality.md</c>.
/// </summary>
public static class PreviewVolumetricRegressionFixtures
{
    public sealed record Pose(
        string Id,
        string Description,
        Vector3 Eye,
        Vector3 LookTarget,
        double LightYawDegrees,
        double LightPitchDegrees,
        float FovDegrees,
        float Aspect,
        float ConeScale,
        float GoldenSunUvX,
        float GoldenSunUvY,
        float GoldenDiscRadiusUv,
        float GoldenConeRadiusUv,
        float GoldenMoonUvX,
        float GoldenMoonUvY,
        int GoldenFingerprint)
    {
        public Vector3 LightDir => PreviewLightMath.LightDirectionFromYawPitch(LightYawDegrees, LightPitchDegrees);

        public (Matrix4x4 View, Matrix4x4 Proj) BuildMatrices()
        {
            var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(Eye, LookTarget, Vector3.UnitY);
            var fovRad = FovDegrees * (MathF.PI / 180f);
            var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(fovRad, Aspect, 0.1f, 500f);
            return (view, proj);
        }

        public int ProjectionFingerprint()
        {
            var (view, proj) = BuildMatrices();
            PreviewSunScreenProjection.Compute(Eye, LightDir, view, proj, Aspect, ConeScale,
                out var sunUv, out var discRadiusUv, out var coneRadiusUv, out _);
            return HashCode.Combine(
                MathF.Round(sunUv.X, 4),
                MathF.Round(sunUv.Y, 4),
                MathF.Round(discRadiusUv, 4),
                MathF.Round(coneRadiusUv, 4));
        }
    }

    private static Pose FromTimeOfDay(
        string id,
        string description,
        Vector3 eye,
        double hours,
        float aspect,
        float coneScale = 1f)
    {
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(hours);
        var lightDir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
        var towardSun = -lightDir;
        if (towardSun.LengthSquared() > 1e-12f)
        {
            towardSun = Vector3.Normalize(towardSun);
        }

        // Orbit camera toward the sun so projection regression stays in-frustum.
        var lookTarget = eye + towardSun * 40f;
        return Build(id, description, eye, lookTarget, yaw, pitch, 45f, aspect, coneScale);
    }

    private static Pose Build(
        string id,
        string description,
        Vector3 eye,
        Vector3 lookTarget,
        double lightYaw,
        double lightPitch,
        float fovDegrees,
        float aspect,
        float coneScale)
    {
        var pose = new Pose(
            id,
            description,
            eye,
            lookTarget,
            lightYaw,
            lightPitch,
            fovDegrees,
            aspect,
            coneScale,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0);
        var (view, proj) = pose.BuildMatrices();
        PreviewSunScreenProjection.Compute(eye, pose.LightDir, view, proj, aspect, coneScale,
            out var sunUv, out var discRadiusUv, out var coneRadiusUv, out _);
        PreviewSunScreenProjection.ComputeMoon(eye, pose.LightDir, view, proj, aspect,
            out var moonUv, out _, out _);
        return pose with
        {
            GoldenSunUvX = sunUv.X,
            GoldenSunUvY = sunUv.Y,
            GoldenDiscRadiusUv = discRadiusUv,
            GoldenConeRadiusUv = coneRadiusUv,
            GoldenMoonUvX = moonUv.X,
            GoldenMoonUvY = moonUv.Y,
            GoldenFingerprint = pose.ProjectionFingerprint()
        };
    }

    /// <summary>Curated poses — golden values captured from <see cref="PreviewSunScreenProjection.Compute"/>.</summary>
    public static IReadOnlyList<Pose> All { get; } =
    [
        Build(
            "default-day-16x9",
            "Default preview sun (-35°/-55°) at 16:9",
            new Vector3(0f, 2f, 6f),
            Vector3.Zero,
            -35.0,
            -55.0,
            45f,
            16f / 9f,
            1f),
        Build(
            "orbit-45-16x9",
            "Camera yaw ~45° orbit, same sun",
            new Vector3(4.24f, 2f, 4.24f),
            Vector3.Zero,
            -35.0,
            -55.0,
            45f,
            16f / 9f,
            1f),
        Build(
            "aspect-21x9",
            "Ultrawide aspect — sun must stay in viewport",
            new Vector3(0f, 2f, 6f),
            Vector3.Zero,
            -35.0,
            -55.0,
            45f,
            21f / 9f,
            1f),
        Build(
            "aspect-4x3",
            "4:3 aspect — sun must stay in viewport",
            new Vector3(0f, 2f, 6f),
            Vector3.Zero,
            -35.0,
            -55.0,
            45f,
            4f / 3f,
            1f),
        FromTimeOfDay("noon-12h", "Solar noon (12 h)", new Vector3(0f, 2f, 6f), 12.0, 16f / 9f),
        FromTimeOfDay("sunset-18h", "Sunset (18 h)", new Vector3(0f, 2f, 6f), 18.0, 16f / 9f),
        FromTimeOfDay("midnight-0h", "Midnight (0 h)", new Vector3(0f, 2f, 6f), 0.0, 16f / 9f),
        Build(
            "cone-wide",
            "Wide god-ray cone scale (1.5×)",
            new Vector3(0f, 2f, 6f),
            Vector3.Zero,
            -35.0,
            -55.0,
            45f,
            16f / 9f,
            1.5f),
    ];

    /// <summary>
    /// Parameters to paste into preview settings for manual screenshot capture (P0.6 hook).
    /// </summary>
    public static string ManualCaptureChecklist() =>
        string.Join(Environment.NewLine, All.Select(p =>
            $"- [{p.Id}] {p.Description}: eye=({p.Eye.X:F2},{p.Eye.Y:F2},{p.Eye.Z:F2}), " +
            $"yaw={p.LightYawDegrees:F1}°, pitch={p.LightPitchDegrees:F1}°, aspect={p.Aspect:F3}, " +
            $"sunUv=({p.GoldenSunUvX:F4},{p.GoldenSunUvY:F4})"));
}
