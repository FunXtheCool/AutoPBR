using Xunit;

namespace AutoPBR.Training.Ort.Tests;

public class VcEdgeChannelTests
{
    [Fact]
    public void Uniform_color_produces_zero_edge_after_normalization()
    {
        var rgb = new byte[3 * 8 * 8];
        Array.Fill(rgb, (byte)200);
        var n = 8;
        var edge = VcEdgeChannel.FromRgb(rgb, n, n);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                Assert.Equal(0f, edge[y, x]);
            }
        }
    }

    [Fact]
    public void Vertical_step_produces_nonzero_edge_golden()
    {
        // 2x2: left column black, right column white
        var rgb = new byte[2 * 2 * 3];
        rgb[0] = 0;
        rgb[1] = 0;
        rgb[2] = 0;
        rgb[3] = 255;
        rgb[4] = 255;
        rgb[5] = 255;
        rgb[6] = 0;
        rgb[7] = 0;
        rgb[8] = 0;
        rgb[9] = 255;
        rgb[10] = 255;
        rgb[11] = 255;

        var edge = VcEdgeChannel.FromRgb(rgb, 2, 2);
        // Golden values generated from the same C# implementation (regression lock for Python parity tooling).
        // Strong step: VC magnitude is identical on all four cells → normalization maps each to 1.
        Assert.Equal(1f, edge[0, 0], precision: 5);
        Assert.Equal(1f, edge[1, 0], precision: 5);
        Assert.Equal(1f, edge[0, 1], precision: 5);
        Assert.Equal(1f, edge[1, 1], precision: 5);
    }
}
