namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Maps equipment parity-catalog builders and texture paths to liftable geometry IR JVM hosts.
/// </summary>
internal static class GeometryIrParityEquipmentJvmMap
{
    public static bool TryResolveOfficialJvm(
        string builderMethod,
        string normalizedAssetPath,
        bool isBaby,
        out string? officialJvm,
        out string? officialJvmBaby)
    {
        officialJvm = null;
        officialJvmBaby = null;
        var path = normalizedAssetPath.Replace('\\', '/');

        if (path.Contains("/textures/entity/equipment/wings/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.object.equipment.ElytraModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/nautilus_body/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.nautilus.NautilusArmorModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/nautilus_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.nautilus.NautilusSaddleModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/camel_saddle/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/camel_husk_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.camel.CamelSaddleModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/horse_saddle/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/donkey_saddle/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/mule_saddle/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/skeleton_horse_saddle/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/zombie_horse_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.equine.EquineSaddleModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/horse_body/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/skeleton_horse_body/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/zombie_horse_body/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.equine.AbstractEquineModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/donkey_body/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/textures/entity/equipment/mule_body/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.equine.DonkeyModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/llama_body/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.llama.LlamaModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/wolf_body/", StringComparison.OrdinalIgnoreCase))
        {
            // 26.1.2 mesh host is AdultWolfModel; WolfModel is a non-mesh interface (skipped shard).
            officialJvm = "net.minecraft.client.model.animal.wolf.AdultWolfModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/humanoid_leggings/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(builderMethod, "EquipmentHumanoidLeggings", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.EquipmentHumanoidLeggingsModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/humanoid_baby/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(builderMethod, "EquipmentHumanoidBaby", StringComparison.OrdinalIgnoreCase) ||
            (isBaby && path.Contains("/textures/entity/equipment/humanoid/", StringComparison.OrdinalIgnoreCase)))
        {
            officialJvm = "net.minecraft.client.model.HumanoidModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/humanoid/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(builderMethod, "EquipmentHumanoid", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.HumanoidModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/pig_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.animal.pig.PigModel";
            return true;
        }

        if (path.Contains("/textures/entity/equipment/strider_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.monster.strider.StriderModel";
            return true;
        }

        return false;
    }
}
