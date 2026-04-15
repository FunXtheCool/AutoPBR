namespace AutoPBR.Training.Ort;

/// <summary>
/// 4th input channel: matches Python <c>ml_specular.edge_channel.vc_edge_from_rgb_uint8</c>.
/// </summary>
public static class VcEdgeChannel
{
    private static int Reflect(int i, int maxV)
    {
        if (i < 0)
        {
            return -i - 1;
        }

        if (i >= maxV)
        {
            return maxV - (i - maxV) - 1;
        }

        return i;
    }

    /// <summary>
    /// rgb: H×W×3 byte RGB. Returns H×W float32 in [0,1].
    /// Uses double accumulation for Sobel / VC sum so flat regions stay numerically zero (parity with NumPy float32 stacks).
    /// </summary>
    public static float[,] FromRgb(ReadOnlySpan<byte> rgb, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgb.Length < width * height * 3)
        {
            throw new ArgumentException("Invalid RGB buffer or dimensions.");
        }

        var lum = new double[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 3;
                var r = rgb[i];
                var g = rgb[i + 1];
                var b = rgb[i + 2];
                lum[y, x] = (r * 0.3 + g * 0.6 + b * 0.1) / 255.0;
            }
        }

        var gx = new double[height, width];
        var gy = new double[height, width];
        ReadOnlySpan<double> kx = stackalloc double[] { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
        ReadOnlySpan<double> ky = stackalloc double[] { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double sx = 0, sy = 0;
                var ki = 0;
                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        var rx = Reflect(x + ox, width);
                        var ry = Reflect(y + oy, height);
                        var v = lum[ry, rx];
                        sx += v * kx[ki];
                        sy += v * ky[ki];
                        ki++;
                    }
                }

                gx[y, x] = sx;
                gy[y, x] = sy;
            }
        }

        const int vcOrientationCount = 12;
        var angleStep = Math.PI / vcOrientationCount;
        var edge = new float[height, width];
        double maxE = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var gxv = gx[y, x];
                var gyv = gy[y, x];
                double s = 0;
                for (var k = 0; k < vcOrientationCount; k++)
                {
                    var a = k * angleStep;
                    var r = gxv * Math.Cos(a) + gyv * Math.Sin(a);
                    s += Math.Abs(r);
                }

                edge[y, x] = (float)s;
                if (s > maxE)
                {
                    maxE = s;
                }
            }
        }

        const double flatEps = 1e-10;
        if (maxE <= flatEps)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    edge[y, x] = 0f;
                }
            }

            return edge;
        }

        var inv = 1.0 / maxE;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                edge[y, x] = Math.Clamp((float)(edge[y, x] * inv), 0f, 1f);
            }
        }

        return edge;
    }
}
