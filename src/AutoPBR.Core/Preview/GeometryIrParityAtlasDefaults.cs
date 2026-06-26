namespace AutoPBR.Core.Preview;

/// <summary>
/// Fallback entity UV atlas sizes for parity-catalog geometry IR when manifest rows, shard fields,
/// and on-disk PNGs are all missing (common for climate/baby mesh hosts until shards are re-lifted).
/// Values align with CleanRoom entity <c>RigBuilder</c> atlas sizes.
/// </summary>
internal static class GeometryIrParityAtlasDefaults
{
    private static readonly Dictionary<string, (int W, int H)> ByBuilderMethod =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cow"] = (64, 64),
            ["Pig"] = (64, 64),
            ["Chicken"] = (64, 32),
            ["Frog"] = (48, 48),
            ["Strider"] = (64, 128),
            ["Creeper"] = (64, 32),
            ["Cod"] = (32, 32),
            ["Salmon"] = (32, 32),
            ["TropicalFishA"] = (32, 32),
            ["TropicalFishB"] = (32, 32),
            ["Bat"] = (64, 64),
            ["Blaze"] = (64, 32),
            ["Ghast"] = (64, 32),
            ["HappyGhast"] = (64, 64),
            ["Horse"] = (64, 64),
            ["DonkeyMuleHorse"] = (64, 64),
            ["Cat"] = (64, 32),
            ["Wolf"] = (64, 32),
            ["HumanoidVillager"] = (64, 64),
            ["HumanoidZombieVillager"] = (64, 64),
            ["WanderingTrader"] = (64, 64),
            ["PlayerWide"] = (64, 64),
            ["PlayerSlim"] = (64, 64),
            ["Humanoid"] = (64, 64),
            ["Bed"] = (64, 64),
            ["StandingSignEntity"] = (64, 32),
            ["HangingSignEntity"] = (64, 32),
            ["DecoratedPotEntity"] = (32, 32),
            ["ConduitEntity"] = (64, 32),
            ["BeaconBeam"] = (16, 256),
            ["BeamColumn"] = (16, 256),
            ["EndPortalSurface"] = (16, 16),
            ["ExperienceOrb"] = (64, 64),
            ["FishingHook"] = (64, 64),
            ["GuardianBeam"] = (16, 256),
            ["DragonFireball"] = (64, 64),
            ["Skull"] = (64, 64),
            ["EquipmentHumanoid"] = (64, 64),
            ["EquipmentHumanoidBaby"] = (64, 64),
            ["EquipmentHumanoidLeggings"] = (64, 64),
            ["EquipmentHorseArmor"] = (64, 64),
            ["EquipmentWolfBody"] = (64, 32),
            ["EquipmentLlamaBody"] = (128, 64),
            ["EquipmentSaddle"] = (64, 64),
            ["EquipmentCamelSaddle"] = (128, 128),
            ["EquipmentNautilusArmor"] = (128, 128),
            ["EquipmentNautilusSaddle"] = (128, 128),
            ["EquipmentWings"] = (64, 32),
        };

    public static bool TryGetForBuilderMethod(string? builderMethod, out int atlasW, out int atlasH)
    {
        atlasW = 0;
        atlasH = 0;
        if (string.IsNullOrWhiteSpace(builderMethod))
        {
            return false;
        }

        if (!ByBuilderMethod.TryGetValue(builderMethod.Trim(), out var size))
        {
            return false;
        }

        atlasW = size.W;
        atlasH = size.H;
        return true;
    }
}
