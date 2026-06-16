namespace AutoPBR.Core.Preview;

/// <summary>
/// Adult humanoids that bake <c>HumanoidModel.createMesh</c> on wrapper JVMs need reference bind-pose
/// repair and stripped body/leg setupAnim when parity preview applies <c>HumanoidModel.setupAnim</c>.
/// </summary>
internal static class GeometryIrHumanoidLayerMeshPreviewPolicy
{
    public static bool IsHumanoidLayerMeshJvm(string? officialJvmName) =>
        officialJvmName is
            "net.minecraft.client.model.monster.zombie.AbstractZombieModel" or
            "net.minecraft.client.model.monster.zombie.ZombieModel" or
            "net.minecraft.client.model.monster.zombie.GiantZombieModel";

    public static bool UsesHumanoidArmPosePreviewPass(string? builderMethod, string? geometryIrOfficialJvm) =>
        string.Equals(builderMethod, "Zombie", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "HumanoidZombie", StringComparison.OrdinalIgnoreCase) ||
        IsHumanoidLayerMeshJvm(geometryIrOfficialJvm);

    public static bool ShouldStripSetupAnimBodyLegHeadChannels(string? builderMethod, string? geometryIrOfficialJvm) =>
        EntityPreviewPoseCatalog.IsPlayerSkinMeshBuilder(builderMethod) ||
        IsHumanoidLayerMeshJvm(geometryIrOfficialJvm);
}
