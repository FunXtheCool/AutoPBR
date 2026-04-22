using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class MlSpecularModelResolutionTests
{
    private static AutoPbrOptions Options(
        bool useMl,
        string? fallback,
        IReadOnlyDictionary<int, string>? map) =>
        new()
        {
            UseMlSpecularPredictor = useMl,
            MlSpecularModelPath = fallback,
            MlSpecularModelPathsByResolution = map
        };

    [Fact]
    public void CeilSmallestKeyGreaterOrEqualTextureSize()
    {
        var map = new Dictionary<int, string>
        {
            [16] = "a.onnx",
            [32] = "b.onnx"
        };
        var o = Options(true, "fallback.onnx", map);
        Assert.True(MlSpecularModelResolution.TryResolveModelPath(o, 8, out var path, out var sel, out _));
        Assert.Equal(16, sel);
        Assert.Equal("a.onnx", path);

        Assert.True(MlSpecularModelResolution.TryResolveModelPath(o, 16, out path, out sel, out _));
        Assert.Equal(16, sel);
        Assert.Equal("a.onnx", path);

        Assert.True(MlSpecularModelResolution.TryResolveModelPath(o, 20, out path, out sel, out _));
        Assert.Equal(32, sel);
        Assert.Equal("b.onnx", path);
    }

    [Fact]
    public void CeilWhenTextureLargerThanAllKeysUsesLargest()
    {
        var map = new Dictionary<int, string>
        {
            [16] = "a.onnx",
            [32] = "b.onnx"
        };
        var o = Options(true, "fallback.onnx", map);
        Assert.True(MlSpecularModelResolution.TryResolveModelPath(o, 128, out var path, out var sel, out _));
        Assert.Equal(32, sel);
        Assert.Equal("b.onnx", path);
    }

    [Fact]
    public void EmptyMapUsesFallbackPath()
    {
        var o = Options(true, "only.onnx", null);
        Assert.True(MlSpecularModelResolution.TryResolveModelPath(o, 64, out var path, out var sel, out _));
        Assert.Null(sel);
        Assert.Equal("only.onnx", path);
    }

    [Fact]
    public void DisabledReturnsFalse()
    {
        var o = Options(false, "x.onnx", new Dictionary<int, string> { [16] = "y.onnx" });
        Assert.False(MlSpecularModelResolution.TryResolveModelPath(o, 16, out _, out _, out var diag));
        Assert.NotNull(diag);
    }

    [Fact]
    public void SanitizeMapIgnoresInvalidEntries()
    {
        var raw = new Dictionary<int, string>
        {
            [16] = "ok.onnx",
            [-1] = "bad.onnx",
            [32] = "  ",
            [64] = "sixty.onnx"
        };
        var s = MlSpecularModelResolution.SanitizeMap(raw);
        Assert.Equal(2, s.Count);
        Assert.Equal("ok.onnx", s[16]);
        Assert.Equal("sixty.onnx", s[64]);
    }
}
