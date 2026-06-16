namespace AutoPBR.Core.Preview;

/// <summary>
/// Preview stand-in for vanilla <c>AbstractIllager.IllagerArmPose</c>
/// (<c>IllagerModel.setupAnim</c>, 26.1.2 <c>client.jar</c>).
/// </summary>
public enum EntityIllagerPreviewArmPose
{
    /// <summary>Separate arms at bind pose (pillager default — no folded-arm UV sheet).</summary>
    ArmsAtSide,
    Crossed,
    AttackingEmptyHands,
    AttackingWeapon,
    Spellcasting,
    BowAndArrow,
    CrossbowHold,
    CrossbowCharge,
    Celebrating,
}
