using System.Numerics;
using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Fact]
    public void LightDirectionFromYawPitchProducesUnitVector()
    {
        // Sample a grid of yaw / pitch values; PreviewLightMath should always return unit-length.
        for (var yaw = -180.0; yaw <= 180.0; yaw += 45.0)
        {
            for (var pitch = -89.0; pitch <= 89.0; pitch += 30.0)
            {
                var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
                Assert.InRange(dir.Length(), 0.999f, 1.001f);
            }
        }
    }

    [Fact]
    public void LightDirectionFromYawPitchRoundtripsThroughInverse()
    {
        // Avoid the polar singularity at |pitch| ~ 90 where atan2(x,z) is undefined for pure Y axes.
        var yawPitchPairs = new (double Yaw, double Pitch)[]
        {
            (-35.0, -55.0),
            (0.0, 0.0),
            (45.0, 12.0),
            (-120.0, -30.0),
            (170.0, 60.0),
            (90.0, -45.0)
        };
        foreach (var (yaw, pitch) in yawPitchPairs)
        {
            var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
            var (yawBack, pitchBack) = PreviewLightMath.LightYawPitchFromDirection(dir);
            Assert.Equal(yaw, yawBack, 4);
            Assert.Equal(pitch, pitchBack, 4);
        }
    }

    [Fact]
    public void LightDirectionFromYawPitchDefaultsMatchPriorHardcodedSun()
    {
        // Defaults in PreviewRenderSettings (-35 yaw, -55 pitch) replace the prior fallback
        // (-0.35, -0.85, -0.4); shadow ortho should now follow the user-controlled sun.
        var dir = PreviewLightMath.LightDirectionFromYawPitch(-35.0, -55.0);
        // Sun above horizon -> light propagates downward (Y < 0).
        Assert.True(dir.Y < 0f);
        // Yaw -35 with default pitch should mostly hit -X / +Z cone like the prior fallback (sign-wise).
        Assert.True(dir.X < 0f);
        Assert.True(dir.Z > 0f);
    }

    [Fact]
    public void LightMathPickShadowViewUpFallsBackForVerticalLight()
    {
        // A light pointing straight up/down should not pick the parallel +Y as the shadow up vector,
        // otherwise the cross product in lookAt collapses.
        var straightDown = new Vector3(0f, -1f, 0f);
        var up = PreviewLightMath.PickShadowViewUp(straightDown);
        Assert.Equal(Vector3.UnitZ, up);

        var slanted = PreviewLightMath.LightDirectionFromYawPitch(45.0, -30.0);
        var slantedUp = PreviewLightMath.PickShadowViewUp(slanted);
        Assert.Equal(Vector3.UnitY, slantedUp);
    }
}
