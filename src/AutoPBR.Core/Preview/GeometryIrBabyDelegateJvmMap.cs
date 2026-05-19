namespace AutoPBR.Core.Preview;

/// <summary>
/// Baby model classes whose <c>createBodyLayer</c> forwards to another mesh host; resolver should prefer the delegate target shard.
/// </summary>
internal static class GeometryIrBabyDelegateJvmMap
{
    private static readonly Dictionary<string, string> DelegateToTarget = new(StringComparer.Ordinal)
    {
        ["net.minecraft.client.model.animal.feline.BabyCatModel"] =
            "net.minecraft.client.model.animal.feline.BabyFelineModel",
        ["net.minecraft.client.model.monster.zombie.BabyDrownedModel"] =
            "net.minecraft.client.model.monster.zombie.BabyZombieModel",
        ["net.minecraft.client.model.monster.piglin.BabyZombifiedPiglinModel"] =
            "net.minecraft.client.model.monster.piglin.BabyPiglinModel",
    };

    public static IEnumerable<string> EnumerateResolutionCandidates(string officialJvmName)
    {
        yield return officialJvmName;
        if (DelegateToTarget.TryGetValue(officialJvmName, out var target))
        {
            yield return target;
        }
    }

    public static bool TryGetDelegateTarget(string babyOfficialJvm, out string targetJvm) =>
        DelegateToTarget.TryGetValue(babyOfficialJvm, out targetJvm!);
}
