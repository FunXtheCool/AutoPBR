namespace AutoPBR.Preview.GeometryIr;

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
        string.Equals(builderMethod, "HumanoidZombieVillager", StringComparison.OrdinalIgnoreCase) ||
        IsHumanoidLayerMeshJvm(geometryIrOfficialJvm);

    /// <summary>
    /// Strip inherited <c>HumanoidModel.setupAnim</c> body/leg/head channels whenever geometry IR uses
    /// flat root-sibling <c>PartPose.offset</c> bakes. That includes repaired
    /// <c>HumanoidModel.createMesh</c> zombies and baby hosts such as
    /// <c>BabyZombieVillagerModel</c> (inherits setupAnim but not the adult mesh bind pose).
    /// </summary>
    public static bool ShouldStripSetupAnimBodyLegHeadChannels(string? builderMethod, string? geometryIrOfficialJvm) =>
        EntityPreviewPoseCatalog.IsPlayerSkinMeshBuilder(builderMethod) ||
        IsHumanoidLayerMeshJvm(geometryIrOfficialJvm) ||
        (UsesInheritedHumanoidSetupAnimPreviewPass(builderMethod, geometryIrOfficialJvm) &&
         !string.IsNullOrWhiteSpace(geometryIrOfficialJvm) &&
         !IsHumanoidLayerMeshJvm(geometryIrOfficialJvm));

    private static bool UsesInheritedHumanoidSetupAnimPreviewPass(string? builderMethod, string? geometryIrOfficialJvm) =>
        EntityPreviewPoseCatalog.IsHumanoidPoseBuilderMethod(builderMethod) ||
        UsesHumanoidArmPosePreviewPass(builderMethod, geometryIrOfficialJvm);
}
