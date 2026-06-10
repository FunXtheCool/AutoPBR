using System.Numerics;

using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewSunScreenProjectionTests
{
    private const float Epsilon = 0.02f;

    [Fact]
    public void Compute_DefaultLightPose_ProjectsSunInsideViewport()
    {
        var eye = new Vector3(0f, 2f, 6f);
        var lookTarget = Vector3.Zero;
        var lightDir = PreviewLightMath.LightDirectionFromYawPitch(-35.0, -55.0);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, lookTarget, Vector3.UnitY);
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            45f * (MathF.PI / 180f), 16f / 9f, 0.1f, 500f);

        PreviewSunScreenProjection.Compute(eye, lightDir, view, proj, 16f / 9f, 1f, 1f,
            out var sunUv, out var discRadiusUv, out var coneRadiusUv, out var cosDiscEdge);

        Assert.InRange(sunUv.X, 0f, 1f);
        Assert.InRange(sunUv.Y, 0f, 1f);
        Assert.True(discRadiusUv > 0.005f);
        Assert.True(cosDiscEdge > 0.85f);
        Assert.True(cosDiscEdge < 1f);
    }

    [Fact]
    public void Compute_FixedMatrices_MatchesGoldenSunUv()
    {
        var eye = new Vector3(1.2f, 3.4f, 8.5f);
        var lookTarget = new Vector3(0f, 1f, 0f);
        var lightDir = PreviewLightMath.LightDirectionFromYawPitch(-35.0, -55.0);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, lookTarget, Vector3.UnitY);
        var aspect = 1.777f;
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            50f * (MathF.PI / 180f), aspect, 0.05f, 400f);

        PreviewSunScreenProjection.Compute(eye, lightDir, view, proj, aspect, 1f, 1f,
            out var sunUv, out var discRadiusUv, out _, out _);

        // Golden values captured from PreviewSunScreenProjection.Compute with the inputs above.
        Assert.Equal(0.5f, sunUv.X, Epsilon);
        Assert.Equal(0.5f, sunUv.Y, Epsilon);
        Assert.Equal(0.008f, discRadiusUv, Epsilon);
    }

    [Fact]
    public void Compute_ConeScale_ScalesShaftRadiusMonotonically()
    {
        var eye = new Vector3(0f, 2f, 6f);
        var lookTarget = Vector3.Zero;
        var lightDir = PreviewLightMath.LightDirectionFromYawPitch(-35.0, -55.0);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, lookTarget, Vector3.UnitY);
        var aspect = 1f;
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            45f * (MathF.PI / 180f), aspect, 0.1f, 500f);

        PreviewSunScreenProjection.Compute(eye, lightDir, view, proj, aspect, 0.5f, 1f,
            out _, out _, out var narrow, out _);
        PreviewSunScreenProjection.Compute(eye, lightDir, view, proj, aspect, 1.5f, 1f,
            out _, out _, out var wide, out _);

        Assert.True(wide > narrow);
    }

    [Fact]
    public void ComputeMoon_AngularSize_SmallerThanSun()
    {
        var pose = PreviewVolumetricRegressionFixtures.All.First(p => p.Id == "midnight-0h");
        var (view, proj) = pose.BuildMatrices();

        PreviewSunScreenProjection.Compute(pose.Eye, pose.LightDir, view, proj, pose.Aspect, pose.ConeScale, 1f,
            out _, out _, out _, out var sunCosDiscEdge);
        PreviewSunScreenProjection.ComputeMoon(pose.Eye, pose.LightDir, view, proj, pose.Aspect,
            out _, out _, out var moonCosDiscEdge);

        Assert.True(PreviewSunScreenProjection.MoonRadius < PreviewSunScreenProjection.SunRadius);
        Assert.True(moonCosDiscEdge > sunCosDiscEdge);
    }

    [Fact]
    public void WorldToViewportUv_CenterOfFrustum_MapsNearCenter()
    {
        var eye = new Vector3(0f, 0f, 5f);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, Vector3.Zero, Vector3.UnitY);
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            60f * (MathF.PI / 180f), 1f, 0.1f, 100f);
        var viewProj = proj * view;

        var uv = PreviewSunScreenProjection.WorldToViewportUv(Vector3.Zero, viewProj);

        Assert.Equal(0.5f, uv.X, Epsilon);
        Assert.Equal(0.5f, uv.Y, Epsilon);
    }
}
