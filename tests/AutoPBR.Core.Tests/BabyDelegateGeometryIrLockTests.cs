using System.Text.Json;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Locks catalog geometry IR for baby classes whose <c>createBodyLayer</c> is a javap-visible forward to another model.
/// </summary>
public sealed class BabyDelegateGeometryIrLockTests
{
    private static string ContentPath(params string[] segments) =>
        Path.Combine([GeometryIrTestTierSupport.FindRepoRoot(), .. segments]);

    private static string RootsJson(string relativeUnderDocsGenerated)
    {
        var path = ContentPath("docs", "generated", relativeUnderDocsGenerated);
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("roots").GetRawText();
    }

    [Fact]
    public void GeometryIr_BabyDrownedRootsMatchBabyZombie_On_26_1_2()
    {
        var drowned = RootsJson(Path.Combine("geometry", "26.1.2", "net.minecraft.client.model.monster.zombie.BabyDrownedModel.json"));
        var zombie = RootsJson(Path.Combine("geometry", "26.1.2", "net.minecraft.client.model.monster.zombie.BabyZombieModel.json"));
        Assert.Equal(zombie, drowned);
    }

    [Fact]
    public void GeometryIr_BabyZombifiedPiglinRootsMatchBabyPiglin_On_26_1_2()
    {
        var zombified = RootsJson(Path.Combine("geometry", "26.1.2", "net.minecraft.client.model.monster.piglin.BabyZombifiedPiglinModel.json"));
        var piglin = RootsJson(Path.Combine("geometry", "26.1.2", "net.minecraft.client.model.monster.piglin.BabyPiglinModel.json"));
        Assert.Equal(piglin, zombified);
    }
}
