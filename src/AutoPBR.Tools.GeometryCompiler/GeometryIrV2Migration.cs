using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Upgrades geometry IR shards to schema v2 lift metadata.</summary>
internal static class GeometryIrV2Migration
{
    private const string LiftExact = "exact";
    private const string LiftDirectionMaskFullBox = "direction_mask_full_box";
    private const string LiftTexCropStatic = "tex_crop_static";

    public static void MigrateDirectory(string geometryVersionDir)
    {
        if (!Directory.Exists(geometryVersionDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(geometryVersionDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                if (node["roots"] is null)
                {
                    continue;
                }

                ApplyToShard(node);
                File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // skip malformed
            }
        }
    }

    public static void ApplyToShard(JsonObject shard)
    {
        if (shard["roots"] is not JsonArray roots)
        {
            return;
        }

        shard["schemaVersion"] = 2;

        foreach (var n in roots)
        {
            if (n is JsonObject part)
            {
                MigratePart(part);
            }
        }

        shard["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(roots);
    }

    private static void MigratePart(JsonObject part)
    {
        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var c in cuboids)
            {
                if (c is JsonObject co)
                {
                    MigrateCuboid(co);
                }
            }
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject cp)
                {
                    MigratePart(cp);
                }
            }
        }
    }

    private static void MigrateCuboid(JsonObject co)
    {
        if (co.ContainsKey("liftKind"))
        {
            return;
        }

        var provenance = (string?)co["provenance"] ?? "";
        if (provenance.Contains("direction_masked_faces_full_box_approx", StringComparison.Ordinal))
        {
            co["liftKind"] = LiftDirectionMaskFullBox;
            co["liftWarnings"] = new JsonArray("direction_mask_unparsed_set");
        }
        else if (co.ContainsKey("uvSpan"))
        {
            co["liftKind"] = LiftTexCropStatic;
        }
        else
        {
            co["liftKind"] = LiftExact;
        }
    }
}
