namespace AutoPBR.Core.Preview;

/// <summary>
/// Maps parity builders whose manifest <c>deobf_model_class</c> is a renderer to hand-lifted geometry IR shard JVM names.
/// Shards live under <c>Data/minecraft-native/geometry/26.1.2/</c> with <c>profile: parity_hand_lift</c>.
/// </summary>
internal static class GeometryIrParityHandLiftJvmMap
{
    public static bool TryGetHandLiftJvm(string builderMethod, string normalizedAssetPath, out string officialJvm)
    {
        officialJvm = "";

        if (string.Equals(builderMethod, "StandingSignEntity", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.SignModel";
            return true;
        }

        if (string.Equals(builderMethod, "HangingSignEntity", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.HangingSignModel";
            return true;
        }

        if (string.Equals(builderMethod, "ConduitEntity", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.ConduitModel";
            return true;
        }

        if (string.Equals(builderMethod, "BeaconBeam", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.BeaconBeamModel";
            return true;
        }

        if (string.Equals(builderMethod, "BeamColumn", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.EndPortalModel";
            return true;
        }

        if (string.Equals(builderMethod, "EndPortalSurface", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.EndPortalSurfaceModel";
            return true;
        }

        if (string.Equals(builderMethod, "ExperienceOrb", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.ExperienceOrbModel";
            return true;
        }

        if (string.Equals(builderMethod, "FishingHook", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.FishingHookModel";
            return true;
        }

        if (string.Equals(builderMethod, "GuardianBeam", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.GuardianBeamModel";
            return true;
        }

        if (string.Equals(builderMethod, "DragonFireball", StringComparison.OrdinalIgnoreCase))
        {
            officialJvm = "net.minecraft.client.model.DragonFireballModel";
            return true;
        }

        return false;
    }
}
