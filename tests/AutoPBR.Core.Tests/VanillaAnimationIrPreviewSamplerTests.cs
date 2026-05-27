namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{
    private static MinecraftNativeProfile TestProfile26 =>
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    private static MinecraftNativeProfile TestProfile111 =>
        new("1.21.11", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "1.21.11"), new Version(1, 21, 11));
}

