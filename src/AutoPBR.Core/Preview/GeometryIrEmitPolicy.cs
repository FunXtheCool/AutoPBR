using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Per-model geometry emit rules (inflate vs UV footprint) from packaged JSON policy.
/// </summary>
public static class GeometryIrEmitPolicy
{
    public enum InflateUvFootprint
    {
        PreInflateIntegerExtents,
        PostInflateMeshExtents
    }

    public static InflateUvFootprint GetInflateUvFootprint(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return InflateUvFootprint.PreInflateIntegerExtents;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native",
                "minecraft_26.1.2_geometry_emit_policy.json");
            if (!File.Exists(path))
            {
                return InflateUvFootprint.PreInflateIntegerExtents;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("by_official_jvm", out var byJvm) &&
                byJvm.TryGetProperty(officialJvmName, out var rule) &&
                rule.TryGetProperty("inflate_uv_footprint", out var footprint))
            {
                var s = footprint.GetString();
                if (string.Equals(s, "post_inflate_mesh_extents", StringComparison.Ordinal))
                {
                    return InflateUvFootprint.PostInflateMeshExtents;
                }
            }
        }
        catch
        {
            // fall through
        }

        return InflateUvFootprint.PreInflateIntegerExtents;
    }
}
