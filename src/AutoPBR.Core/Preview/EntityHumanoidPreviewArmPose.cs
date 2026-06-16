namespace AutoPBR.Core.Preview;

/// <summary>
/// Preview stand-in for vanilla <c>HumanoidModel.ArmPose</c>
/// (<c>HumanoidModel.setupAnim</c>, 26.1.2 <c>client.jar</c>).
/// </summary>
public enum EntityHumanoidPreviewArmPose
{
    Empty,
    Item,
    Block,
    BowAndArrow,
    CrossbowHold,
    CrossbowCharge,
    Spyglass,
    /// <summary>Zombie-family arms-forward idle (zombie villager default).</summary>
    ZombieArms,
}
