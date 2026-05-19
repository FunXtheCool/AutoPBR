// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static class GeometryIrParityEmitPresetRegistry
    {
        private static readonly HashSet<string> HumanoidLeggingsParts = new(StringComparer.OrdinalIgnoreCase)
        {
            "body",
            "left_leg",
            "right_leg",
        };

        public static GeometryIrMeshEmitOptions CreateEmitOptions(
            string builderMethod,
            MinecraftNativeProfile profile,
            bool isBaby,
            string? resolvedOfficialJvm,
            int atlasW,
            int atlasH,
            float idlePhase01,
            float wave)
        {
            _ = idlePhase01;
            _ = wave;
            var p = ParityCatalogDefaultBabyProfile(profile, isBaby, resolvedOfficialJvm);
            var opts = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with
            {
                DefaultPartScale = p.BodyScale,
                ResolvePartScale = partId => ResolveDefaultPartScale(partId, p),
            };

            if (string.Equals(builderMethod, "EquipmentHumanoidLeggings", StringComparison.OrdinalIgnoreCase))
            {
                return opts with { IncludePartIds = HumanoidLeggingsParts };
            }

            return opts;
        }
    }
}
