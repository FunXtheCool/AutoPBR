namespace AutoPBR.Core.Tests;

internal static class VanillaJsonFixture
{
    internal static string Root =>
        Path.Combine(AppContext.BaseDirectory, "Data", "vanilla-26.1.2");

    internal static DirectoryAssetSource OpenSource() => new(Root);
}
