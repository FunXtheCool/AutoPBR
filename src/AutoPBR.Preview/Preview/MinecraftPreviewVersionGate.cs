namespace AutoPBR.Preview;

/// <summary>
/// Bounds when legacy (1.21.11) preview assets and IR may be used versus modern (26.1.x) data.
/// </summary>
internal static class MinecraftPreviewVersionGate
{
    public const string LegacyNativeProfileLabel = "1.21.11";

    public static readonly Version LegacyPreviewCeiling = new(1, 21, 11);

    public static bool IsLegacyGameVersion(Version version) => version <= LegacyPreviewCeiling;

    public static bool IsModernGameVersion(Version version) => version > LegacyPreviewCeiling;
}
