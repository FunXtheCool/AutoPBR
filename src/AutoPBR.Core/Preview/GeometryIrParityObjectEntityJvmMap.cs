namespace AutoPBR.Core.Preview;

/// <summary>
/// Path-aware geometry IR JVM routing for block-linked object entities (boats, chests, banners, skull, etc.).
/// Prefer committed bytecode shards over hand-lift fallbacks; use variant JVM rows for multi-factory hosts.
/// </summary>
internal static class GeometryIrParityObjectEntityJvmMap
{
    public static IEnumerable<string> EnumerateCandidates(string builderMethod, string normalizedAssetPath)
    {
        var path = normalizedAssetPath.Replace('\\', '/');

        if (string.Equals(builderMethod, "Boat", StringComparison.OrdinalIgnoreCase))
        {
            if (PathIsBambooBoat(path))
            {
                yield return "net.minecraft.client.model.object.boat.RaftModel";
            }
            else
            {
                yield return "net.minecraft.client.model.object.boat.BoatModel";
            }

            yield break;
        }

        if (string.Equals(builderMethod, "ChestBoat", StringComparison.OrdinalIgnoreCase))
        {
            if (PathIsBambooChestBoat(path))
            {
                yield return "net.minecraft.client.model.object.boat.RaftModel.createChestRaftModel";
            }
            else
            {
                yield return "net.minecraft.client.model.object.boat.BoatModel.createChestBoatModel";
            }

            yield break;
        }

        if (string.Equals(builderMethod, "ChestEntity", StringComparison.OrdinalIgnoreCase))
        {
            if (PathIsChestDoubleLeft(path))
            {
                yield return "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyLeftLayer";
            }
            else if (PathIsChestDoubleRight(path))
            {
                yield return "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyRightLayer";
            }
            else
            {
                yield return "net.minecraft.client.model.object.chest.ChestModel";
            }

            yield break;
        }

        if (string.Equals(builderMethod, "Minecart", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.object.cart.MinecartModel";
            yield break;
        }

        if (string.Equals(builderMethod, "Bell", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.object.bell.BellModel";
            yield break;
        }

        if (string.Equals(builderMethod, "BannerFlagStanding", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.object.banner.BannerFlagModel.standingPreviewComposite";
            yield break;
        }

        if (string.Equals(builderMethod, "BannerFlagWall", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.object.banner.BannerFlagModel.wallPreviewComposite";
            yield break;
        }

        if (string.Equals(builderMethod, "Skull", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.object.skull.SkullModel.previewComposite";
            yield break;
        }

        if (string.Equals(builderMethod, "DecoratedPotEntity", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.DecoratedPotModel.previewComposite";
            yield break;
        }

        if (string.Equals(builderMethod, "Bed", StringComparison.OrdinalIgnoreCase))
        {
            yield return "net.minecraft.client.model.BedModel.previewComposite";
        }
    }

    private static bool PathIsBambooBoat(string path) =>
        path.Contains("/textures/entity/boat/bamboo", StringComparison.OrdinalIgnoreCase);

    private static bool PathIsBambooChestBoat(string path) =>
        path.Contains("/textures/entity/chest_boat/bamboo", StringComparison.OrdinalIgnoreCase);

    private static bool PathIsChestDoubleLeft(string path) =>
        path.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("_left", StringComparison.OrdinalIgnoreCase);

    private static bool PathIsChestDoubleRight(string path) =>
        path.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("_right", StringComparison.OrdinalIgnoreCase);
}
