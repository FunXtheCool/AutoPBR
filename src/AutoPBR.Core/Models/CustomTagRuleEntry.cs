using System.Text.Json.Serialization;

namespace AutoPBR.Core.Models;

/// <summary>User-defined tag rule (settings + optional JSON file for CLI).</summary>
public sealed class CustomTagRuleEntry
{
    /// <summary>When false, this rule is excluded from effective rules (merge and legend).</summary>
    public bool Enabled { get; set; } = true;

    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>Comma-separated keywords; match when texture name or path contains any (case-insensitive).</summary>
    public string Keywords { get; set; } = "";
    public bool InvertHeight { get; set; }
    public bool InvertSpecular { get; set; }
    public bool InvertNormalRed { get; set; }
    public bool InvertNormalGreen { get; set; }

    /// <summary>When set, scales normal strength for matching textures (same range as global normal intensity, e.g. 0.85).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float? NormalIntensity { get; set; }

    /// <summary>When set, scales height strength for matching textures (e.g. 0.07 for subtler height).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float? HeightIntensity { get; set; }

    /// <summary>When true/false, forces fast/full specular path for matching textures.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FastSpecular { get; set; }

    public TagRule ToTagRule()
    {
        var keywords = string.IsNullOrWhiteSpace(Keywords)
            ? []
            : Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToList();
        var o = new TextureOverrides
        {
            InvertHeight = InvertHeight,
            InvertSpecular = InvertSpecular,
            InvertNormalRed = InvertNormalRed,
            InvertNormalGreen = InvertNormalGreen,
            NormalIntensity = NormalIntensity,
            HeightIntensity = HeightIntensity,
            FastSpecular = FastSpecular
        };
        return new TagRule
        {
            Id = Id.Trim(),
            DisplayName = DisplayName.Trim(),
            Keywords = keywords,
            Overrides = o
        };
    }
}
