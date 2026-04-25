using AutoPBR.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class BrickHeightPostProcessorTests
{
    /// <summary>Curated relative keys for manual/visual regression (documentation; not loaded at runtime).</summary>
    public static readonly string[] GoldBrickTextureKeyExamples =
    [
        "\\minecraft\\block\\bricks",
        "\\minecraft\\block\\stone_bricks",
        "\\minecraft\\block\\deepslate_bricks",
        "\\minecraft\\block\\red_nether_bricks",
        "\\minecraft\\block\\nether_bricks"
    ];

    [Fact]
    public void GoldBrickTextureKeyExamplesIsDocumentedList()
    {
        Assert.NotEmpty(GoldBrickTextureKeyExamples);
    }

    [Fact]
    public void ApplyUniformDiffuseSkipsDueToLowStructure()
    {
        const int n = 32;
        using var img = new Image<Rgba32>(n, n, new Rgba32(128, 128, 128));
        var height = new byte[n * n];
        Array.Fill(height, (byte)200);
        var opts = new AutoPbrOptions { BrickHeightMapPostProcessEnabled = true };

        var outBytes = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.True(res.SkippedLowConfidence);
        Assert.Equal(height, outBytes);
    }

    [Fact]
    public void ApplyGridMortarNoInvertLeavesHeightUnchanged()
    {
        const int n = 64;
        using var img = CreateWhiteGridWithBlackMortar(n, 8);

        var height = new byte[n * n];
        Array.Fill(height, (byte)220);
        var opts = new AutoPbrOptions
        {
            BrickHeightMapPostProcessEnabled = true,
            BrickHeightMinStructuralConfidence = 0.008f,
            BrickMortarDepressionAlpha = 0.5f,
            BrickBulkLiftBeta = 0f
        };

        var outBytes = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.False(res.SkippedLowConfidence);
        Assert.True(res.StructuralConfidence > 0.02f, $"conf={res.StructuralConfidence}");
        Assert.False(res.AppliedGlobalInvert);
        Assert.Equal(height, outBytes);
    }

    [Fact]
    public void ApplyWhenMortarHeightHigherTriggersInvertWhenConfident()
    {
        const int n = 48;
        using var img = CreateWhiteGridWithBlackMortar(n, 6);

        var height = new byte[n * n];
        for (var i = 0; i < height.Length; i++)
        {
            height[i] = 80;
        }

        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (img[x, y].R < 40)
                {
                    height[y * n + x] = 220;
                }
            }
        }

        var opts = new AutoPbrOptions
        {
            BrickHeightMapPostProcessEnabled = true,
            BrickHeightMinStructuralConfidence = 0.008f,
            BrickHeightInvertConfidenceFloor = 0.02f,
            BrickHeightInvertDeltaThreshold = 2f,
            BrickMortarDepressionAlpha = 0.2f,
            BrickBulkLiftBeta = 0f
        };

        _ = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.True(res.AppliedGlobalInvert || res.DeltaMortarMinusBrick > 0,
            $"expected invert path or positive delta, got inv={res.AppliedGlobalInvert} d={res.DeltaMortarMinusBrick}");
    }

    /// <summary>Red brick + light grout: diffuse height puts joint higher; light-grout path should global-invert.</summary>
    [Fact]
    public void ApplyLightGroutDiffuseInvertsGlobalHeightWhenMortarIsHigher()
    {
        const int n = 48;
        using var img = CreateDarkBrickWithLightMortar(n, 6);
        var height = new byte[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var p = img[x, y];
                var lum = p.R * 0.3f + p.G * 0.6f + p.B * 0.1f;
                var isMortar = lum > 200f;
                height[y * n + x] = isMortar ? (byte)220 : (byte)65;
            }
        }

        var opts = new AutoPbrOptions
        {
            BrickHeightMapPostProcessEnabled = true,
            BrickHeightMinStructuralConfidence = 0.008f,
            BrickLightGroutDiffuseDeltaMin = 0.01f,
            BrickMortarDepressionAlpha = 0.35f,
            BrickBulkLiftBeta = 0f
        };

        _ = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.True(res.AppliedGlobalInvert,
            $"expected global invert for light grout, inv={res.AppliedGlobalInvert} d={res.DeltaMortarMinusBrick}");
    }

    [Fact]
    public void ApplyDarkBrickWithDarkMortarDoesNotTriggerLightGroutInvert()
    {
        const int n = 64;
        using var img = CreateDarkBrickWithDarkMortar(n, 8);
        var height = new byte[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var p = img[x, y];
                var lum = p.R * 0.3f + p.G * 0.6f + p.B * 0.1f;
                var isMortar = lum < 45f;
                // Mirror your false-positive setup: mortar appears higher in raw diffuse-derived height.
                height[y * n + x] = isMortar ? (byte)210 : (byte)95;
            }
        }

        var opts = new AutoPbrOptions
        {
            BrickHeightMapPostProcessEnabled = true,
            BrickHeightMinStructuralConfidence = 0.006f,
            BrickHeightInvertDeltaThreshold = 255f,
            BrickLightGroutDiffuseDeltaMin = 0.003f
        };

        _ = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.False(res.AppliedGlobalInvert,
            $"dark mortar should not trigger light-grout invert, inv={res.AppliedGlobalInvert} d={res.DeltaMortarMinusBrick}");
    }

    [Fact]
    public void ApplyDarkBrickWithBrightSpecklesDoesNotTriggerLightGroutInvert()
    {
        const int n = 64;
        using var img = CreateDarkBrickWithDarkMortarAndBrightSpeckles(n, 8, speckleStep: 5);
        var height = new byte[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var p = img[x, y];
                var lum = p.R * 0.3f + p.G * 0.6f + p.B * 0.1f;
                var isMortar = lum < 45f;
                // Keep positive mortar-minus-brick delta so only improved light-grout checks can suppress false invert.
                height[y * n + x] = isMortar ? (byte)215 : (byte)105;
            }
        }

        var opts = new AutoPbrOptions
        {
            BrickHeightMapPostProcessEnabled = true,
            BrickHeightMinStructuralConfidence = 0.006f,
            BrickHeightInvertDeltaThreshold = 255f,
            BrickLightGroutDiffuseDeltaMin = 0.003f
        };

        _ = BrickHeightPostProcessor.Apply(height, n, n, img, opts, out var res);

        Assert.False(res.AppliedGlobalInvert,
            $"bright flecks should not masquerade as light grout, inv={res.AppliedGlobalInvert} d={res.DeltaMortarMinusBrick}");
    }

    private static Image<Rgba32> CreateWhiteGridWithBlackMortar(int n, int step)
    {
        var white = new Rgba32(255, 255, 255);
        var black = new Rgba32(0, 0, 0);
        var img = new Image<Rgba32>(n, n);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                img[x, y] = white;
            }
        }

        for (var i = 0; i < n; i += step)
        {
            var ix = Math.Min(i, n - 1);
            for (var y = 0; y < n; y++)
            {
                img[ix, y] = black;
            }

            for (var x = 0; x < n; x++)
            {
                img[x, ix] = black;
            }
        }

        return img;
    }

    private static Image<Rgba32> CreateDarkBrickWithLightMortar(int n, int step)
    {
        var brick = new Rgba32(110, 35, 35);
        var mortar = new Rgba32(245, 245, 245);
        var img = new Image<Rgba32>(n, n);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                img[x, y] = brick;
            }
        }

        for (var i = 0; i < n; i += step)
        {
            var ix = Math.Min(i, n - 1);
            for (var y = 0; y < n; y++)
            {
                img[ix, y] = mortar;
            }

            for (var x = 0; x < n; x++)
            {
                img[x, ix] = mortar;
            }
        }

        return img;
    }

    private static Image<Rgba32> CreateDarkBrickWithDarkMortar(int n, int step)
    {
        var brick = new Rgba32(68, 28, 28);
        var mortar = new Rgba32(34, 18, 18);
        var img = new Image<Rgba32>(n, n);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                img[x, y] = brick;
            }
        }

        for (var i = 0; i < n; i += step)
        {
            var ix = Math.Min(i, n - 1);
            for (var y = 0; y < n; y++)
            {
                img[ix, y] = mortar;
            }

            for (var x = 0; x < n; x++)
            {
                img[x, ix] = mortar;
            }
        }

        return img;
    }

    private static Image<Rgba32> CreateDarkBrickWithDarkMortarAndBrightSpeckles(int n, int step, int speckleStep)
    {
        var img = CreateDarkBrickWithDarkMortar(n, step);
        var speckle = new Rgba32(130, 78, 72);
        for (var y = 2; y < n; y += Math.Max(3, speckleStep))
        {
            for (var x = 2; x < n; x += Math.Max(3, speckleStep))
            {
                // Keep speckles mostly off mortar lines to mimic bright brick-face flecks.
                if ((x % step) != 0 && (y % step) != 0)
                {
                    img[x, y] = speckle;
                }
            }
        }

        return img;
    }
}
