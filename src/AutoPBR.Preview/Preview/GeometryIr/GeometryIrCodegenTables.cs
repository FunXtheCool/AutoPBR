using AutoPBR.Preview.Generated;

namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Maps official JVM names to compile-time <see cref="EntityModelRuntime.EntityCuboid"/> body-layer tables.
/// </summary>
internal static class GeometryIrCodegenTables
{
    public static bool TryGetBodyLayerSpan(string officialJvmName, out ReadOnlySpan<EntityModelRuntime.EntityCuboid> cuboids)
    {
        cuboids = default;
        if (string.Equals(officialJvmName, "net.minecraft.client.model.animal.fish.CodModel", StringComparison.Ordinal))
        {
            cuboids = GeometryIrEntityCuboidTables.CodModelBodyLayer;
            return true;
        }

        if (string.Equals(officialJvmName, "net.minecraft.client.model.animal.fish.SalmonModel", StringComparison.Ordinal))
        {
            cuboids = GeometryIrEntityCuboidTables.SalmonModelBodyLayer;
            return true;
        }

        if (string.Equals(officialJvmName, "net.minecraft.client.model.animal.chicken.ChickenModel", StringComparison.Ordinal))
        {
            cuboids = GeometryIrEntityCuboidTables.ChickenModelBodyLayer;
            return true;
        }

        return false;
    }
}
