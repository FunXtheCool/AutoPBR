using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed partial class BindingGapPilotMeshLiftTests
{
    private static JsonObject? FindPart(JsonArray roots, string id)
    {
        JsonObject? found = null;
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (string.Equals((string?)p["id"], id, StringComparison.Ordinal))
                {
                    found = p;
                    return;
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return found;
    }

    private static List<string> CollectPartIds(JsonArray roots)
    {
        var ids = new List<string>();
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (p["id"] is JsonValue id)
                {
                    ids.Add(id.GetValue<string>()!);
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return ids;
    }
}
