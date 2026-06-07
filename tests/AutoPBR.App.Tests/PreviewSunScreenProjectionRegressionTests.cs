using System.Numerics;

using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

/// <summary>P0.6 — regression golden projections for fixed camera/light poses.</summary>
public sealed class PreviewSunScreenProjectionRegressionTests
{
    private const float Epsilon = 0.025f;

    public static IEnumerable<object[]> AllPoseData =>
        PreviewVolumetricRegressionFixtures.All.Select(static p => new object[] { p });

    [Theory]
    [MemberData(nameof(AllPoseData))]
    public void Compute_FixturePose_SunStaysInsideViewport(PreviewVolumetricRegressionFixtures.Pose pose)
    {
        var (view, proj) = pose.BuildMatrices();
        PreviewSunScreenProjection.Compute(pose.Eye, pose.LightDir, view, proj, pose.Aspect, pose.ConeScale,
            out var sunUv, out var discRadiusUv, out var coneRadiusUv, out _);

        Assert.InRange(sunUv.X, 0f, 1f);
        Assert.InRange(sunUv.Y, 0f, 1f);
        Assert.True(discRadiusUv >= 0.008f);
        Assert.True(coneRadiusUv >= discRadiusUv);
    }

    [Theory]
    [MemberData(nameof(AllPoseData))]
    public void Compute_FixturePose_MatchesGoldenProjection(PreviewVolumetricRegressionFixtures.Pose pose)
    {
        var (view, proj) = pose.BuildMatrices();
        PreviewSunScreenProjection.Compute(pose.Eye, pose.LightDir, view, proj, pose.Aspect, pose.ConeScale,
            out var sunUv, out var discRadiusUv, out var coneRadiusUv, out _);

        Assert.Equal(pose.GoldenSunUvX, sunUv.X, Epsilon);
        Assert.Equal(pose.GoldenSunUvY, sunUv.Y, Epsilon);
        Assert.Equal(pose.GoldenDiscRadiusUv, discRadiusUv, Epsilon);
        Assert.Equal(pose.GoldenConeRadiusUv, coneRadiusUv, Epsilon);
    }

    [Theory]
    [MemberData(nameof(AllPoseData))]
    public void Compute_FixturePose_MatchesGoldenFingerprint(PreviewVolumetricRegressionFixtures.Pose pose)
    {
        Assert.Equal(pose.GoldenFingerprint, pose.ProjectionFingerprint());
    }

    [Fact]
    public void AllFixtures_HaveUniqueIds()
    {
        var ids = PreviewVolumetricRegressionFixtures.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AllPoseData))]
    public void ComputeMoon_FixturePose_MatchesGoldenProjection(PreviewVolumetricRegressionFixtures.Pose pose)
    {
        var (view, proj) = pose.BuildMatrices();
        PreviewSunScreenProjection.ComputeMoon(pose.Eye, pose.LightDir, view, proj, pose.Aspect,
            out var moonUv, out _, out _);

        Assert.Equal(pose.GoldenMoonUvX, moonUv.X, Epsilon);
        Assert.Equal(pose.GoldenMoonUvY, moonUv.Y, Epsilon);
    }

    [Fact]
    public void ComputeMoon_MidnightPose_IsOppositeSun()
    {
        var pose = PreviewVolumetricRegressionFixtures.All.First(p => p.Id == "midnight-0h");
        var (view, proj) = pose.BuildMatrices();
        PreviewSunScreenProjection.Compute(pose.Eye, pose.LightDir, view, proj, pose.Aspect, 1f,
            out var sunUv, out _, out _, out _);
        PreviewSunScreenProjection.ComputeMoon(pose.Eye, pose.LightDir, view, proj, pose.Aspect,
            out var moonUv, out _, out _);

        Assert.InRange(Vector2.Distance(sunUv, moonUv), 0.3f, 1.2f);
    }

    [Fact]
    public void ManualCaptureChecklist_IsNonEmpty()
    {
        var checklist = PreviewVolumetricRegressionFixtures.ManualCaptureChecklist();
        Assert.Contains("default-day-16x9", checklist);
        Assert.Contains("aspect-21x9", checklist);
    }
}
