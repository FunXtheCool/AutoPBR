namespace AutoPBR.Core.Preview;

/// <summary>
/// Catalog of alternate idle preview poses for entity diffuse textures (Explore pose selector).
/// </summary>
public static class EntityPreviewPoseCatalog
{
    public const string IllagerCrossed = "illager.crossed";
    public const string IllagerAttackingEmptyHands = "illager.attacking_empty_hands";
    public const string IllagerAttackingWeapon = "illager.attacking_weapon";
    public const string IllagerSpellcasting = "illager.spellcasting";
    public const string IllagerBowAndArrow = "illager.bow_and_arrow";
    public const string IllagerCrossbowHold = "illager.crossbow_hold";
    public const string IllagerCrossbowCharge = "illager.crossbow_charge";
    public const string IllagerCelebrating = "illager.celebrating";

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

    public static bool TryGetPoseOptions(
        string normalizedAssetPath,
        string? builderMethod,
        out IReadOnlyList<EntityPreviewPoseOption> options)
    {
        options = [];
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            !IsIllagerBuilderMethod(builderMethod))
        {
            return false;
        }

        var defaultPose = ResolveDefaultIllagerArmPose(norm, builderMethod);
        options = IllagerPoseOptions
            .Select(o => o with { IsDefault = PoseIdMatchesArmPose(o.Id, defaultPose) })
            .ToArray();
        return true;
    }

    public static EntityIllagerPreviewArmPose ResolveEffectiveIllagerArmPose(
        string normalizedAssetPath,
        string? builderMethod,
        string? selectedPoseId)
    {
        if (TryParsePoseId(selectedPoseId, out var selected))
        {
            return selected;
        }

        return ResolveDefaultIllagerArmPose(
            normalizedAssetPath.Replace('\\', '/').TrimStart('/'),
            builderMethod);
    }

    public static bool TryParsePoseId(string? poseId, out EntityIllagerPreviewArmPose armPose)
    {
        armPose = default;
        if (string.IsNullOrWhiteSpace(poseId))
        {
            return false;
        }

        armPose = poseId switch
        {
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

        return poseId is IllagerCrossed or IllagerAttackingEmptyHands or IllagerAttackingWeapon
            or IllagerSpellcasting or IllagerBowAndArrow or IllagerCrossbowHold or IllagerCrossbowCharge
            or IllagerCelebrating;
    }

    internal static bool IsIllagerBuilderMethod(string? builderMethod) =>
        string.Equals(builderMethod, "Evoker", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Vindicator", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Pillager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Illager", StringComparison.OrdinalIgnoreCase);

    private static EntityIllagerPreviewArmPose ResolveDefaultIllagerArmPose(
        string normalizedAssetPath,
        string? builderMethod)
    {
        if (string.Equals(builderMethod, "Evoker", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/illager/evoker", StringComparison.OrdinalIgnoreCase))
        {
            return EntityIllagerPreviewArmPose.Spellcasting;
        }

        if (string.Equals(builderMethod, "Vindicator", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/illager/vindicator", StringComparison.OrdinalIgnoreCase))
        {
            return EntityIllagerPreviewArmPose.AttackingWeapon;
        }

        if (string.Equals(builderMethod, "Pillager", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/illager/pillager", StringComparison.OrdinalIgnoreCase))
        {
            return EntityIllagerPreviewArmPose.CrossbowHold;
        }

        return EntityIllagerPreviewArmPose.Crossed;
    }

    private static bool PoseIdMatchesArmPose(string poseId, EntityIllagerPreviewArmPose armPose) =>
        TryParsePoseId(poseId, out var parsed) && parsed == armPose;
}
