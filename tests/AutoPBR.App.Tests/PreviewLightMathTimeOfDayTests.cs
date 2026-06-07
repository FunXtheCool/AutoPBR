using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewLightMathTimeOfDayTests
{
    [Theory]
    [InlineData(6, 0.0)]
    [InlineData(12, -58.0, 0.5)]
    [InlineData(18, 0.0)]
    [InlineData(0, 58.0, 0.5)]
    public void LightYawPitchFromTimeOfDay_MatchesExpectedSolarElevation(double hours, double expectedPitch, double epsilon = 0.75)
    {
        var (_, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(hours);
        Assert.Equal(expectedPitch, pitch, epsilon);
    }

    [Theory]
    [InlineData(6.0)]
    [InlineData(12.0)]
    [InlineData(18.0)]
    [InlineData(0.0)]
    public void TimeOfDayFromLightYawPitch_RoundtripsKeyHours(double hours)
    {
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(hours);
        var roundTrip = PreviewLightMath.TimeOfDayFromLightYawPitch(yaw, pitch, hours);
        Assert.Equal(hours, roundTrip, 0.75);
    }

    [Theory]
    [InlineData(6.0)]
    [InlineData(12.0)]
    [InlineData(18.0)]
    [InlineData(0.0)]
    public void TimeOfDay_ToYawPitch_ProducesUnitLightDirection(double hours)
    {
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(hours);
        var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
        Assert.InRange(dir.Length(), 0.999f, 1.001f);
    }

    [Fact]
    public void TimeOfDay_NoonLightPointsDownward()
    {
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(12.0);
        var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
        Assert.True(dir.Y < 0f);
        _ = yaw;
        _ = pitch;
    }

    [Fact]
    public void TimeOfDay_MidnightLightPointsUpward()
    {
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(0.0);
        var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
        Assert.True(dir.Y > 0f);
        _ = yaw;
        _ = pitch;
    }

    [Fact]
    public void EffectiveTimeOfDayHours_AdvancesWithRenderTime()
    {
        var settings = new PreviewRenderSettings
        {
            TimeOfDayHours = 12f,
            AnimateTimeOfDay = true,
            TimeOfDaySpeed = 2f
        };

        Assert.Equal(14f, PreviewLightMath.EffectiveTimeOfDayHours(settings, 1.0), 0.01f);
    }

    [Fact]
    public void EffectiveLightYawPitch_UsesStaticYawPitchWhenAnimationOff()
    {
        var settings = new PreviewRenderSettings
        {
            LightYawDegrees = -20f,
            LightPitchDegrees = -40f,
            AnimateTimeOfDay = false
        };

        var (yaw, pitch) = PreviewLightMath.EffectiveLightYawPitch(settings, 99.0);
        Assert.Equal(-20.0, yaw);
        Assert.Equal(-40.0, pitch);
    }
}
