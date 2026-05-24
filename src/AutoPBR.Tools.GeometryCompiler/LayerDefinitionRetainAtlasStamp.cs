using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Stamps per-cuboid <c>textureWidth</c>/<c>textureHeight</c> and <c>textureKey</c> from
/// <c>PartDefinition.retainPartsAndChildren</c> + <c>LayerDefinition.create</c> on supplementary factory islands
/// (e.g. Breeze <c>createWindLayer</c> 128×128 → <c>#wind</c>).
/// Cuboids are lifted from shared <c>createBaseMesh</c>; atlas operands live on thin wrapper factories only.
/// </summary>
internal static partial class LayerDefinitionRetainAtlasStamp
{
    private sealed record RetainAtlasRule(
        HashSet<string> RootPartIds,
        int TextureWidth,
        int TextureHeight,
        string TextureKey);

    [GeneratedRegex(@"ldc\s+#\d+\s+//\s+String\s+(\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex LdcPartNameRegex();

    public static void ApplyToLiftedRoots(JsonArray roots, string meshConcat)
    {
        var rules = ParseRetainAtlasRules(meshConcat);
        if (rules.Count == 0)
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is JsonObject ro)
            {
                StampPartSubtree(ro, rules, inheritedRule: null);
            }
        }
    }

    /// <summary>Shard-level atlas for the primary <c>createBodyLayer</c> retain pass (first retain+create island).</summary>
    public static bool TryReadPrimaryRetainFactoryAtlas(string meshConcat, out int textureWidth, out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        foreach (var rule in ParseRetainAtlasRules(meshConcat))
        {
            textureWidth = rule.TextureWidth;
            textureHeight = rule.TextureHeight;
            return true;
        }

        return false;
    }

    private static List<RetainAtlasRule> ParseRetainAtlasRules(string meshConcat)
    {
        var list = new List<RetainAtlasRule>();
        if (string.IsNullOrWhiteSpace(meshConcat))
        {
            return list;
        }

        var islands = meshConcat.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker,
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in islands)
        {
            var island = raw.Trim();
            if (island.Length == 0 ||
                !island.Contains("retainPartsAndChildren", StringComparison.Ordinal) ||
                !LayerDefinitionAtlasSizeProbe.TryRead(island, out var w, out var h))
            {
                continue;
            }

            var partIds = ParseRetainedPartIdsBeforeRetain(island);
            if (partIds.Count == 0)
            {
                continue;
            }

            list.Add(new RetainAtlasRule(partIds, w, h, ResolveTextureKey(island, partIds)));
        }

        return list;
    }

    /// <summary>Layer texture keys aligned with parity emit (<c>#wind</c> / <c>#eyes</c> / <c>#skin</c>).</summary>
    private static string ResolveTextureKey(string islandBytecode, HashSet<string> partIds)
    {
        if (islandBytecode.Contains("createWindLayer", StringComparison.Ordinal))
        {
            return "#wind";
        }

        if (islandBytecode.Contains("createEyesLayer", StringComparison.Ordinal))
        {
            return "#eyes";
        }

        foreach (var id in partIds)
        {
            if (id.StartsWith("wind", StringComparison.Ordinal))
            {
                return "#wind";
            }
        }

        if (partIds.Contains("eyes"))
        {
            return "#eyes";
        }

        return "#skin";
    }

    private static HashSet<string> ParseRetainedPartIdsBeforeRetain(string islandBytecode)
    {
        var retainIdx = islandBytecode.IndexOf("retainPartsAndChildren", StringComparison.Ordinal);
        if (retainIdx < 0)
        {
            return [];
        }

        var scan = islandBytecode[..retainIdx];
        var folded = string.Join('\n',
            JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
                scan.Split('\n').Select(l => l.TrimEnd('\r')).ToList()));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in LdcPartNameRegex().Matches(folded))
        {
            var id = m.Groups[1].Value;
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static void StampPartSubtree(JsonObject part, IReadOnlyList<RetainAtlasRule> rules, RetainAtlasRule? inheritedRule)
    {
        var partId = (string?)part["id"] ?? "";
        var rule = inheritedRule;
        if (partId.Length > 0)
        {
            foreach (var candidate in rules)
            {
                if (candidate.RootPartIds.Contains(partId))
                {
                    rule = candidate;
                    break;
                }
            }
        }

        if (rule is not null && part["cuboids"] is JsonArray cuboids)
        {
            foreach (var c in cuboids)
            {
                if (c is JsonObject cuboid)
                {
                    cuboid["textureWidth"] = rule.TextureWidth;
                    cuboid["textureHeight"] = rule.TextureHeight;
                    cuboid["textureKey"] = rule.TextureKey;
                }
            }
        }

        if (part["children"] is not JsonArray children)
        {
            return;
        }

        foreach (var ch in children)
        {
            if (ch is JsonObject child)
            {
                StampPartSubtree(child, rules, rule ?? inheritedRule);
            }
        }
    }
}
