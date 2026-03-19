namespace AutoPBR.Core.Models;

/// <summary>
/// Built-in tag rules applied when options.TagRules is null or empty.
/// </summary>
public static class TagRulePresets
{
    /// <summary>
    /// Default tag rules. E.g. "brick": invert height and specular; "wood": invert height for bark relief.
    /// </summary>
    public static IReadOnlyList<TagRule> Default { get; } =
    [
        new TagRule
        {
            Id = "brick",
            DisplayName = "Brick",
            Keywords = ["brick"],
            Overrides = new TextureOverrides
            {
                InvertHeight = true,
                InvertSpecular = true
            }
        },
        new TagRule
        {
            Id = "wood",
            DisplayName = "Wood",
            Keywords = ["wood", "plank", "log", "bark"],
            Overrides = new TextureOverrides
            {
                InvertHeight = true
            }
        },
        new TagRule
        {
            Id = "metal",
            DisplayName = "Metal",
            Keywords = ["metal", "ore", "gold", "iron", "copper", "ingot", "chain", "netherite", "nugget"],
            Overrides = new TextureOverrides
            {
                NormalIntensity = 0.85f
            }
        },
        new TagRule
        {
            Id = "foliage",
            DisplayName = "Foliage",
            Keywords = ["leaves", "grass", "vine", "fern", "flower", "sapling", "kelp", "lily", "mushroom"],
            Overrides = new TextureOverrides
            {
                HeightIntensity = 0.07f
            }
        }
    ];

    /// <summary>Returns tag ids whose keywords match the texture name or relative key (case-insensitive).</summary>
    public static IReadOnlyList<string> GetMatchingTagIds(string name, string relativeKey, IReadOnlyList<TagRule> rules) =>
        TagRuleApplicator.GetMatchingTagIds(name, relativeKey, rules);
}
