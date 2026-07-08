using System.Text.Json.Nodes;

namespace AutoPBR.Preview;

/// <summary>
/// Resolves lifted setupAnim <c>playbackSteps</c> animation fields to bytecode animation IR when
/// <c>bakedAnimations</c> wiring was not lifted from the model class static initializer.
/// </summary>
internal static class SetupAnimPlaybackDefinitionResolver
{
    public static JsonObject? TryResolve(string parityBuilderMethod, string animationField, bool isBaby)
    {
        if (string.IsNullOrWhiteSpace(parityBuilderMethod) || string.IsNullOrWhiteSpace(animationField))
        {
            return null;
        }

        JsonObject? best = null;
        var bestScore = -1;
        foreach (var binding in EntityParityAnimationMap.GetBindingsForParityBuilder(parityBuilderMethod))
        {
            if (binding.RestrictToBabyTextures is { } restrict && restrict != isBaby)
            {
                continue;
            }

            if (!VanillaAnimationIrPreviewSampler.TryGetAnimationRoot(null, binding.AnimationOfficialJvmName, out var root) ||
                root["definitions"] is not JsonArray defs)
            {
                continue;
            }

            foreach (var node in defs)
            {
                if (node is not JsonObject def)
                {
                    continue;
                }

                var fieldName = (string?)def["fieldName"] ?? "";
                var score = ScoreDefinitionMatch(fieldName, animationField);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = def;
                }
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static int ScoreDefinitionMatch(string definitionFieldName, string modelAnimationField)
    {
        var def = definitionFieldName.ToUpperInvariant();
        var stem = modelAnimationField.EndsWith("Animation", StringComparison.Ordinal)
            ? modelAnimationField[..^9]
            : modelAnimationField;
        var stemUpper = stem.ToUpperInvariant();

        if (string.Equals(stemUpper, "SITPOSE", StringComparison.Ordinal) ||
            string.Equals(stemUpper, "SIT_POSE", StringComparison.Ordinal))
        {
            return def.Contains("SIT", StringComparison.Ordinal) && def.Contains("POSE", StringComparison.Ordinal) ? 100 : 0;
        }

        if (string.Equals(stemUpper, "SIT", StringComparison.Ordinal))
        {
            if (def.Contains("SIT", StringComparison.Ordinal) && def.Contains("POSE", StringComparison.Ordinal))
            {
                return 0;
            }

            return def.Contains("SIT", StringComparison.Ordinal) ? 90 : 0;
        }

        if (string.Equals(stemUpper, "STANDUP", StringComparison.Ordinal))
        {
            return def.Contains("STAND", StringComparison.Ordinal) && def.Contains("UP", StringComparison.Ordinal) ? 90 : 0;
        }

        var compactStem = stemUpper.Replace("_", "", StringComparison.Ordinal);
        if (compactStem.Length == 0)
        {
            return 0;
        }

        return def.Contains(compactStem, StringComparison.Ordinal) ? compactStem.Length + 10 : 0;
    }
}
