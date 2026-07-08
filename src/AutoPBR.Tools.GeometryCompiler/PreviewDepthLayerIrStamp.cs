using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Stamps <c>previewDepthLayer</c> on lifted cuboids using the same resolver rules as runtime IR emit.
/// </summary>
internal static class PreviewDepthLayerIrStamp
{
    public static void ApplyToLiftedRoots(JsonArray roots, string? officialJvmName)
    {
        foreach (var root in roots)
        {
            if (root is JsonObject part)
            {
                StampPartSubtree(part, officialJvmName);
            }
        }
    }

    private static void StampPartSubtree(JsonObject part, string? officialJvmName)
    {
        var partId = (string?)part["id"] ?? "";
        if (part["cuboids"] is JsonArray cuboids)
        {
            var count = cuboids.Count;
            for (var i = 0; i < count; i++)
            {
                if (cuboids[i] is not JsonObject cuboid || cuboid.ContainsKey("previewDepthLayer"))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(cuboid.ToJsonString());
                var (kind, _, _) = PreviewDepthLayerResolver.ClassifyIrCuboid(
                    partId,
                    officialJvmName,
                    doc.RootElement,
                    i,
                    count);
                if (kind == PreviewDepthLayerKind.Base)
                {
                    continue;
                }

                cuboid["previewDepthLayer"] = GeometryIrCuboidMetadata.ToPreviewDepthLayerJsonName(kind);
            }
        }

        if (part["children"] is not JsonArray children)
        {
            return;
        }

        foreach (var child in children)
        {
            if (child is JsonObject childPart)
            {
                StampPartSubtree(childPart, officialJvmName);
            }
        }
    }
}
