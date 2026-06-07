using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewFramebufferFingerprintTests
{
    [Fact]
    public void Compute_SamePixels_ProducesStableHash()
    {
        var pixels = new byte[16 * 16 * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = 40;
            pixels[i + 1] = 80;
            pixels[i + 2] = 120;
        }

        var a = PreviewFramebufferFingerprint.Compute(pixels, 16, 16);
        var b = PreviewFramebufferFingerprint.Compute(pixels, 16, 16);
        Assert.Equal(a, b);
        Assert.NotEqual(0, a);
    }

    [Fact]
    public void Compute_DifferentPixels_ProducesDifferentHash()
    {
        var left = new byte[16 * 16 * 3];
        var right = new byte[16 * 16 * 3];
        left[0] = 10;
        right[0] = 20;
        Assert.NotEqual(
            PreviewFramebufferFingerprint.Compute(left, 16, 16),
            PreviewFramebufferFingerprint.Compute(right, 16, 16));
    }
}
