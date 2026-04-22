using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class MlSpecularModelMapParsingTests
{
    [Fact]
    public void ParsesResolutionAndPath()
    {
        Assert.True(MlSpecularModelMapParsing.TryParseMapEntry("16=C:\\models\\m.onnx", out var res, out var path, out var err));
        Assert.Equal(16, res);
        Assert.Equal(@"C:\models\m.onnx", path);
        Assert.Null(err);
    }

    [Fact]
    public void RejectsMissingEquals()
    {
        Assert.False(MlSpecularModelMapParsing.TryParseMapEntry("16xfile.onnx", out _, out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void RejectsEmptyPath()
    {
        Assert.False(MlSpecularModelMapParsing.TryParseMapEntry("32=", out _, out _, out var err));
        Assert.NotNull(err);
    }
}
