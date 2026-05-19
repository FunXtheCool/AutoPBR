using AutoPBR.Core.Preview.Generated;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Maps official JVM names to compile-time <see cref="CleanRoomEntityModelRuntime.EntityCuboid"/> body-layer tables.
/// </summary>
internal static class GeometryIrCodegenTables
{
    public static bool TryGetBodyLayerSpan(string officialJvmName, out ReadOnlySpan<CleanRoomEntityModelRuntime.EntityCuboid> cuboids)
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
