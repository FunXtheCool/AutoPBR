namespace AutoPBR.Preview;

/// <summary>
/// Catalog of alternate idle preview poses for entity diffuse textures (Explore pose selector).
/// </summary>
public static class EntityPreviewPoseCatalog
{
    public const string IllagerArmsAtSide = "illager.arms_at_side";
    public const string IllagerCrossed = "illager.crossed";
    public const string IllagerAttackingEmptyHands = "illager.attacking_empty_hands";
    public const string IllagerAttackingWeapon = "illager.attacking_weapon";
    public const string IllagerSpellcasting = "illager.spellcasting";
    public const string IllagerBowAndArrow = "illager.bow_and_arrow";
    public const string IllagerCrossbowHold = "illager.crossbow_hold";
    public const string IllagerCrossbowCharge = "illager.crossbow_charge";
    public const string IllagerCelebrating = "illager.celebrating";

    public const string HumanoidEmpty = "humanoid.empty";
    public const string HumanoidItem = "humanoid.item";
    public const string HumanoidBlock = "humanoid.block";
    public const string HumanoidBowAndArrow = "humanoid.bow_and_arrow";
    public const string HumanoidCrossbowHold = "humanoid.crossbow_hold";
    public const string HumanoidCrossbowCharge = "humanoid.crossbow_charge";
    public const string HumanoidSpyglass = "humanoid.spyglass";
    public const string HumanoidZombieArms = "humanoid.zombie_arms";

    private static readonly EntityPreviewPoseOption[] IllagerPoseOptions =
    [
        new(IllagerCrossed, "Crossed arms", IsDefault: false),
        new(IllagerAttackingEmptyHands, "Attacking (empty hands)", IsDefault: false),
        new(IllagerAttackingWeapon, "Attacking (weapon)", IsDefault: false),
        new(IllagerSpellcasting, "Spellcasting", IsDefault: false),
        new(IllagerBowAndArrow, "Bow and arrow", IsDefault: false),
        new(IllagerCrossbowHold, "Crossbow hold", IsDefault: false),
        new(IllagerCrossbowCharge, "Crossbow charge", IsDefault: false),
        new(IllagerCelebrating, "Celebrating", IsDefault: false),
    ];

    private static readonly EntityPreviewPoseOption PillagerArmsAtSideOption =
        new(IllagerArmsAtSide, "Arms at side", IsDefault: false);

    private static readonly EntityPreviewPoseOption[] HumanoidPoseOptions =
    [
        new(HumanoidEmpty, "Arms at side", IsDefault: false),
        new(HumanoidItem, "Holding item", IsDefault: false),
        new(HumanoidBlock, "Holding block", IsDefault: false),
        new(HumanoidBowAndArrow, "Bow and arrow", IsDefault: false),
        new(HumanoidCrossbowHold, "Crossbow hold", IsDefault: false),
        new(HumanoidCrossbowCharge, "Crossbow charge", IsDefault: false),
        new(HumanoidSpyglass, "Spyglass", IsDefault: false),
    ];

    private static readonly EntityPreviewPoseOption HumanoidZombieArmsOption =
        new(HumanoidZombieArms, "Zombie arms", IsDefault: false);

    public static bool TryGetPoseOptions(
        string normalizedAssetPath,
        string? builderMethod,
        out IReadOnlyList<EntityPreviewPoseOption> options)
    {
        options = [];
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsHumanoidPoseBuilderMethod(builderMethod))
        {
            var defaultPose = ResolveDefaultHumanoidArmPose(builderMethod);
            var source = string.Equals(builderMethod, "HumanoidZombieVillager", StringComparison.OrdinalIgnoreCase)
                ? HumanoidPoseOptions.Prepend(HumanoidZombieArmsOption)
                : HumanoidPoseOptions;
            options = source
                .Select(o => o with { IsDefault = HumanoidPoseIdMatchesArmPose(o.Id, defaultPose) })
                .ToArray();
            return true;
        }

        if (!IsIllagerBuilderMethod(builderMethod))
        {
            return false;
        }

        var defaultIllagerPose = ResolveDefaultIllagerArmPose(norm, builderMethod);
        var illagerSource = IsPillagerTexture(norm, builderMethod)
            ? IllagerPoseOptions
                .Where(o => !string.Equals(o.Id, IllagerCrossed, StringComparison.Ordinal))
                .Prepend(PillagerArmsAtSideOption)
            : IllagerPoseOptions;
        options = illagerSource
            .Select(o => o with { IsDefault = IllagerPoseIdMatchesArmPose(o.Id, defaultIllagerPose) })
            .ToArray();
        return true;
    }

    public static EntityIllagerPreviewArmPose ResolveEffectiveIllagerArmPose(
        string normalizedAssetPath,
        string? builderMethod,
        string? selectedPoseId)
    {
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (TryParseIllagerPoseId(selectedPoseId, out var selected))
        {
            if (selected == EntityIllagerPreviewArmPose.Crossed && IsPillagerTexture(norm, builderMethod))
            {
                return EntityIllagerPreviewArmPose.ArmsAtSide;
            }

            return selected;
        }

        return ResolveDefaultIllagerArmPose(norm, builderMethod);
    }

    public static EntityHumanoidPreviewArmPose ResolveEffectiveHumanoidArmPose(
        string? builderMethod,
        string? selectedPoseId)
    {
        if (TryParseHumanoidPoseId(selectedPoseId, out var selected))
        {
            return selected;
        }

        return ResolveDefaultHumanoidArmPose(builderMethod);
    }

    public static bool TryParseIllagerPoseId(string? poseId, out EntityIllagerPreviewArmPose armPose)
    {
        armPose = default;
        if (string.IsNullOrWhiteSpace(poseId))
        {
            return false;
        }

        armPose = poseId switch
        {
            IllagerArmsAtSide => EntityIllagerPreviewArmPose.ArmsAtSide,
            IllagerCrossed => EntityIllagerPreviewArmPose.Crossed,
            IllagerAttackingEmptyHands => EntityIllagerPreviewArmPose.AttackingEmptyHands,
            IllagerAttackingWeapon => EntityIllagerPreviewArmPose.AttackingWeapon,
            IllagerSpellcasting => EntityIllagerPreviewArmPose.Spellcasting,
            IllagerBowAndArrow => EntityIllagerPreviewArmPose.BowAndArrow,
            IllagerCrossbowHold => EntityIllagerPreviewArmPose.CrossbowHold,
            IllagerCrossbowCharge => EntityIllagerPreviewArmPose.CrossbowCharge,
            IllagerCelebrating => EntityIllagerPreviewArmPose.Celebrating,
            _ => default,
        };

        return poseId is IllagerArmsAtSide or IllagerCrossed or IllagerAttackingEmptyHands or IllagerAttackingWeapon
            or IllagerSpellcasting or IllagerBowAndArrow or IllagerCrossbowHold or IllagerCrossbowCharge
            or IllagerCelebrating;
    }

    public static bool TryParseHumanoidPoseId(string? poseId, out EntityHumanoidPreviewArmPose armPose)
    {
        armPose = default;
        if (string.IsNullOrWhiteSpace(poseId))
        {
            return false;
        }

        armPose = poseId switch
        {
            HumanoidEmpty => EntityHumanoidPreviewArmPose.Empty,
            HumanoidItem => EntityHumanoidPreviewArmPose.Item,
            HumanoidBlock => EntityHumanoidPreviewArmPose.Block,
            HumanoidBowAndArrow => EntityHumanoidPreviewArmPose.BowAndArrow,
            HumanoidCrossbowHold => EntityHumanoidPreviewArmPose.CrossbowHold,
            HumanoidCrossbowCharge => EntityHumanoidPreviewArmPose.CrossbowCharge,
            HumanoidSpyglass => EntityHumanoidPreviewArmPose.Spyglass,
            HumanoidZombieArms => EntityHumanoidPreviewArmPose.ZombieArms,
            _ => default,
        };

        return poseId is HumanoidEmpty or HumanoidItem or HumanoidBlock or HumanoidBowAndArrow
            or HumanoidCrossbowHold or HumanoidCrossbowCharge or HumanoidSpyglass or HumanoidZombieArms;
    }

    internal static bool IsIllagerBuilderMethod(string? builderMethod) =>
        string.Equals(builderMethod, "Evoker", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Vindicator", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Pillager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Illager", StringComparison.OrdinalIgnoreCase);

    internal static bool IsHumanoidPoseBuilderMethod(string? builderMethod) =>
        string.Equals(builderMethod, "PlayerHumanoid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "PlayerWide", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "PlayerSlim", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "HumanoidGeneric", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "HumanoidZombieVillager", StringComparison.OrdinalIgnoreCase);

    internal static bool IsPlayerSkinMeshBuilder(string? builderMethod) =>
        string.Equals(builderMethod, "PlayerWide", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "PlayerSlim", StringComparison.OrdinalIgnoreCase);

    private static EntityIllagerPreviewArmPose ResolveDefaultIllagerArmPose(
        string normalizedAssetPath,
        string? builderMethod) =>
        IsPillagerTexture(normalizedAssetPath, builderMethod)
            ? EntityIllagerPreviewArmPose.ArmsAtSide
            : EntityIllagerPreviewArmPose.Crossed;

    private static EntityHumanoidPreviewArmPose ResolveDefaultHumanoidArmPose(string? builderMethod) =>
        string.Equals(builderMethod, "HumanoidZombieVillager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Zombie", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "HumanoidZombie", StringComparison.OrdinalIgnoreCase)
            ? EntityHumanoidPreviewArmPose.ZombieArms
            : EntityHumanoidPreviewArmPose.Empty;

    private static bool IsPillagerTexture(string normalizedAssetPath, string? builderMethod) =>
        string.Equals(builderMethod, "Pillager", StringComparison.OrdinalIgnoreCase) ||
        normalizedAssetPath.Contains("/illager/pillager", StringComparison.OrdinalIgnoreCase);

    private static bool IllagerPoseIdMatchesArmPose(string poseId, EntityIllagerPreviewArmPose armPose) =>
        TryParseIllagerPoseId(poseId, out var parsed) && parsed == armPose;

    private static bool HumanoidPoseIdMatchesArmPose(string poseId, EntityHumanoidPreviewArmPose armPose) =>
        TryParseHumanoidPoseId(poseId, out var parsed) && parsed == armPose;
}
