using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Normalizes geometry IR root trees for stable jar-lift reconciliation compares.
/// </summary>
internal static class GeometryIrRootsCanonicalizer
{
    private static readonly JsonSerializerOptions CanonicalJson = new() { WriteIndented = false };

    public static string Canonicalize(JsonArray roots) =>
        JsonSerializer.Serialize(Normalize(roots), CanonicalJson);

    private static JsonArray Normalize(JsonArray roots)
    {
        var clone = JsonNode.Parse(roots.ToJsonString())!.AsArray();
        Walk(clone);
        return clone;
    }

    private static void Walk(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                o.Remove("setupAnimPivot");
                foreach (var prop in o.ToList())
                {
                    if (prop.Value is JsonValue jv && jv.TryGetValue<double>(out var d))
                    {
                        o[prop.Key] = JsonValue.Create(Math.Round(d, 4, MidpointRounding.AwayFromZero));
                    }
                    else
                    {
                        Walk(prop.Value);
                    }
                }

                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (a[i] is JsonValue jv && jv.TryGetValue<double>(out var d))
                    {
                        a[i] = JsonValue.Create(Math.Round(d, 4, MidpointRounding.AwayFromZero));
                    }
                    else
                    {
                        Walk(a[i]);
                    }
                }

                break;
        }
    }
}
