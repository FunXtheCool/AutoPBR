using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class OpenGlRgbaUploadTests
{
    [Fact]
    public void EnsureBottomRowFirst_flips_top_row_to_bottom()
    {
        var rgba = new byte[]
        {
            255, 0, 0, 255,
            0, 255, 0, 255,
        };
        var flipped = OpenGlRgbaUpload.EnsureBottomRowFirst(rgba, 1, 2);
        Assert.Equal((byte)0, flipped[0]);
        Assert.Equal((byte)255, flipped[4]);
    }
}
