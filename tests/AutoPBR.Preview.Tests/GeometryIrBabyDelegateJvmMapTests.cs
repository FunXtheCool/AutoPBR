namespace AutoPBR.Core.Tests;

public sealed class GeometryIrBabyDelegateJvmMapTests
{
    [Fact]
    public void Adult_cat_resolves_to_adult_feline_shard()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        const string adultCat = "net.minecraft.client.model.animal.feline.AdultCatModel";
        var candidates = GeometryIrBabyDelegateJvmMap.EnumerateResolutionCandidates(adultCat).ToList();
        Assert.Contains("net.minecraft.client.model.animal.feline.AdultFelineModel", candidates);
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(
            profile,
            "net.minecraft.client.model.animal.feline.AdultFelineModel",
            out _));
    }

    [Fact]
    public void Baby_cat_resolves_to_baby_feline_shard()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        const string babyCat = "net.minecraft.client.model.animal.feline.BabyCatModel";
        var candidates = GeometryIrBabyDelegateJvmMap.EnumerateResolutionCandidates(babyCat).ToList();
        Assert.Contains("net.minecraft.client.model.animal.feline.BabyFelineModel", candidates);
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(
            profile,
            "net.minecraft.client.model.animal.feline.BabyFelineModel",
            out _));
    }

    [Fact]
    public void Baby_drowned_resolves_to_baby_zombie_shard()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        const string babyDrowned = "net.minecraft.client.model.monster.zombie.BabyDrownedModel";
        var candidates = GeometryIrBabyDelegateJvmMap.EnumerateResolutionCandidates(babyDrowned).ToList();
        Assert.Contains("net.minecraft.client.model.monster.zombie.BabyZombieModel", candidates);
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(
            profile,
            "net.minecraft.client.model.monster.zombie.BabyZombieModel",
            out _));
    }
}
