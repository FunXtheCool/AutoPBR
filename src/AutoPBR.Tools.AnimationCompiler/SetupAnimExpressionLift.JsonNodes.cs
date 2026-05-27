using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimExpressionLift
{
    internal static JsonObject PartPeerNode(string peerPartField, string property) =>
        new()
        {
            ["partPeer"] = peerPartField,
            ["peerProperty"] = property
        };

    internal static JsonObject ConstNode(float v) => new() { ["const"] = v };

    internal static JsonObject StateNode(string field) => new() { ["state"] = field };

    internal static JsonObject OpNode(string op, params JsonObject[] args)
    {
        var arr = new JsonArray();
        foreach (var a in args)
        {
            arr.Add(CloneExpr(a));
        }

        return new JsonObject
        {
            ["op"] = op,
            ["args"] = arr
        };
    }

    internal static JsonObject CloneExpr(JsonObject o) => JsonNode.Parse(o.ToJsonString())!.AsObject();

    private static bool TryResolveLocalFromPriorGetfield(
          List<string> lines,
          int floadLineIdx,
          string localName,
          IReadOnlyDictionary<string, float>? modelAccessors,
          out JsonObject expr)
    {
        expr = new JsonObject();
        if (!IsFLocalName(localName))
        {
            return false;
        }

        for (var i = floadLineIdx - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (TryParseStoreLocal(line, out var storeName) && storeName == localName)
            {
                if (TryLiftConditionalFloatStore(lines, i, out var conditional))
                {
                    expr = conditional;
                    return true;
                }

                if (TryLiftAssignmentExprFromStoreSimple(lines, i, out var stored, modelAccessors) && stored.Count > 0)
                {
                    expr = stored;
                    return true;
                }

                for (var j = i - 1; j >= Math.Max(0, i - 8); j--)
                {
                    var m = RenderStateFieldRegex.Match(lines[j]);
                    if (m.Success)
                    {
                        expr = StateNode(m.Groups[1].Value);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsFLocalName(string localName) =>
        Regex.IsMatch(localName, @"^f\d+$", RegexOptions.None, TimeSpan.FromSeconds(1));
}
