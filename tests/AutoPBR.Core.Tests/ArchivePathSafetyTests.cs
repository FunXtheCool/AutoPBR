using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class ArchivePathSafetyTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/block/stone.png")]
    [InlineData("pack.mcmeta")]
    [InlineData("assets\\minecraft\\textures\\block\\stone.png")]
    public void TryResolveExtractionPath_AllowsRelativeArchivePaths(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "AutoPBR", "safe_extract");

        Assert.True(ArchivePathSafety.TryResolveExtractionPath(root, entry, out var destination));
        Assert.StartsWith(Path.GetFullPath(root), destination, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("assets/../../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("assets/..\\escape.txt")]
    public void TryResolveExtractionPath_RejectsTraversalAndRootedPaths(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "AutoPBR", "safe_extract");

        Assert.False(ArchivePathSafety.TryResolveExtractionPath(root, entry, out _));
    }
}
