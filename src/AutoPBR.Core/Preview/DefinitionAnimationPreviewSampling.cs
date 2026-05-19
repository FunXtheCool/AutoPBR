using System.Numerics;
using System.Text.Json.Nodes;


namespace AutoPBR.Core.Preview;

/// <summary>
/// Samples lifted <c>AnimationDefinition</c> channels for preview (replaces per-entity <c>TrySample*</c> APIs).
/// </summary>
internal static partial class DefinitionAnimationPreviewSampling
{
    public static bool SampleRotationDegrees(
        MinecraftNativeProfile? profile,
        string animationOfficialJvmName,
        string definitionField,
        string partName,
        float timeSeconds,
        out Vector3 eulerDegrees)
    {
        eulerDegrees = default;
        if (!TryGetDefinition(profile, animationOfficialJvmName, definitionField, out var def))
        {
            return false;
        }

        return TrySampleDegreesLinear(def, partName, timeSeconds, out eulerDegrees);
    }

    public static bool SamplePosition(
        MinecraftNativeProfile? profile,
        string animationOfficialJvmName,
        string definitionField,
        string partName,
        float timeSeconds,
        out Vector3 translation)
    {
        translation = default;
        if (!TryGetDefinition(profile, animationOfficialJvmName, definitionField, out var def))
        {
            return false;
        }

        return TrySamplePositionLinear(def, partName, timeSeconds, out translation);
    }

    public static bool SampleScale(
        MinecraftNativeProfile? profile,
        string animationOfficialJvmName,
        string definitionField,
        string partName,
        float timeSeconds,
        out Vector3 scale)
    {
        scale = Vector3.One;
        if (!TryGetDefinition(profile, animationOfficialJvmName, definitionField, out var def) ||
            def["channels"] is not JsonArray channels)
        {
            return false;
        }

        foreach (var ch in channels)
        {
            if (ch is not JsonObject co ||
                !string.Equals((string?)co["partName"], partName, StringComparison.Ordinal) ||
                !string.Equals((string?)co["target"], "SCALE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return VanillaAnimationIrPreviewSampler.TrySampleChannel(co, timeSeconds, "SCALE", out scale);
        }

        return false;
    }

    private static bool TryGetDefinition(
        MinecraftNativeProfile? _,
        string animationOfficialJvmName,
        string definitionField,
        out JsonObject definition) =>
        VanillaAnimationIrPreviewSampler.TryGetDefinitionByClassField(animationOfficialJvmName, definitionField, out definition);

    private static bool TrySampleDegreesLinear(JsonObject definition, string partName, float timeSeconds, out Vector3 eulerDeg) =>
        VanillaAnimationIrPreviewSampler.TrySampleDegreesLinear(definition, partName, timeSeconds, out eulerDeg);

    private static bool TrySamplePositionLinear(JsonObject definition, string partName, float timeSeconds, out Vector3 v) =>
        VanillaAnimationIrPreviewSampler.TrySamplePositionLinear(definition, partName, timeSeconds, out v);
}
