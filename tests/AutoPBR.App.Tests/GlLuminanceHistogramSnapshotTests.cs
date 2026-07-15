using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class GlLuminanceHistogramSnapshotTests
{
    [Fact]
    public void FromRgb8_UsesStableIntegerLuminanceBins()
    {
        byte[] rgb =
        [
            0, 0, 0,
            255, 255, 255,
            255, 0, 0,
            0, 255, 0,
        ];

        var snapshot = GlLuminanceHistogramSnapshot.FromRgb8(rgb, 4, 1);

        Assert.Equal(4u, snapshot.SampleCount);
        Assert.Equal(0u, snapshot.OverflowCount);
        Assert.Equal(1u, snapshot.Bins[0]);
        Assert.Equal(1u, snapshot.Bins[13]);
        Assert.Equal(1u, snapshot.Bins[45]);
        Assert.Equal(1u, snapshot.Bins[63]);
        Assert.True(snapshot.IsConsistent);
    }

    [Fact]
    public void ResolveSampleStride_BoundsLargeImagesToCapacity()
    {
        var stride = GlLuminanceHistogramSnapshot.ResolveSampleStride(3840, 2160, 65_536);
        var samples = ((3840 + stride - 1) / stride) * ((2160 + stride - 1) / stride);

        Assert.True(samples <= 65_536);
        Assert.True(stride > 1);
    }
}
