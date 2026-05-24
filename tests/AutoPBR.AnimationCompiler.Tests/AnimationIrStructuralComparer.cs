using System.Text.Json.Nodes;

namespace AutoPBR.AnimationCompiler.Tests;

internal static class AnimationIrStructuralComparer
{
    public static (bool IsMatch, string? Message) CompareDefinitions(JsonArray committed, JsonArray lifted)
    {
        if (committed.Count != lifted.Count)
        {
            return (false, $"definition count committed={committed.Count} lifted={lifted.Count}");
        }

        var committedByField = IndexDefinitionsByFieldName(committed);
        var liftedByField = IndexDefinitionsByFieldName(lifted);

        if (committedByField.Count != liftedByField.Count)
        {
            return (false,
                $"unique fieldName count committed={committedByField.Count} lifted={liftedByField.Count}");
        }

        foreach (var field in committedByField.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!liftedByField.TryGetValue(field, out var liftedDef))
            {
                return (false, $"missing lifted definition {field}");
            }

            var committedDef = committedByField[field];
            if (committedDef.TryGetPropertyValue("lengthSeconds", out var committedLen) &&
                liftedDef.TryGetPropertyValue("lengthSeconds", out var liftedLen))
            {
                var c = committedLen!.GetValue<double>();
                var l = liftedLen!.GetValue<double>();
                if (Math.Abs(c - l) > 1e-4)
                {
                    return (false, $"{field}: lengthSeconds committed={c} lifted={l}");
                }
            }

            var cmp = CompareChannelSignatures(committedDef, liftedDef, field);
            if (!cmp.IsMatch)
            {
                return cmp;
            }
        }

        return (true, null);
    }

    private static (bool IsMatch, string? Message) CompareChannelSignatures(
        JsonObject committedDef,
        JsonObject liftedDef,
        string fieldName)
    {
        var committedSigs = ChannelSignatures(committedDef);
        var liftedSigs = ChannelSignatures(liftedDef);
        if (committedSigs.Count != liftedSigs.Count)
        {
            return (false,
                $"{fieldName}: channel count committed={committedSigs.Count} lifted={liftedSigs.Count}");
        }

        committedSigs.Sort(StringComparer.Ordinal);
        liftedSigs.Sort(StringComparer.Ordinal);
        for (var i = 0; i < committedSigs.Count; i++)
        {
            if (!string.Equals(committedSigs[i], liftedSigs[i], StringComparison.Ordinal))
            {
                return (false, $"{fieldName}: channel[{i}] committed={committedSigs[i]} lifted={liftedSigs[i]}");
            }
        }

        return (true, null);
    }

    private static List<string> ChannelSignatures(JsonObject definition)
    {
        var list = new List<string>();
        if (definition["channels"] is not JsonArray channels)
        {
            return list;
        }

        foreach (var ch in channels)
        {
            if (ch is not JsonObject co)
            {
                continue;
            }

            var part = (string?)co["partName"] ?? "";
            var target = (string?)co["target"] ?? "";
            var interp = (string?)co["interpolation"] ?? "";
            var kfCount = co["keyframes"] is JsonArray kf ? kf.Count : 0;
            list.Add($"{part}|{target}|{interp}|{kfCount}");
        }

        return list;
    }

    private static Dictionary<string, JsonObject> IndexDefinitionsByFieldName(JsonArray definitions)
    {
        var map = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var n in definitions)
        {
            if (n is not JsonObject def)
            {
                continue;
            }

            var field = (string?)def["fieldName"];
            if (!string.IsNullOrEmpty(field))
            {
                map[field] = def;
            }
        }

        return map;
    }
}
