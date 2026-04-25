namespace AutoPBR.Core.Models;

using RuleExpressions;

/// <summary>
/// Built-in material and flag tags when options.TagRules is null or empty.
/// </summary>
public static class TagRulePresets
{
    /// <summary>Default material tags (keywords + optional semantic hints for MiniLM).</summary>
    public static IReadOnlyList<TagRule> DefaultMaterials { get; } =
    [
        new TagRule
        {
            Id = "brick",
            DisplayName = "Brick",
            Kind = TagRuleKind.Material,
            Keywords = ["brick"],
            SemanticHints = ["Clay", "Masonry"]
        },
        new TagRule
        {
            Id = "wood",
            DisplayName = "Wood",
            Kind = TagRuleKind.Material,
            Keywords = ["wood", "plank", "log", "bark", "tree"],
            SemanticHints =
            [
                "lumber wooden planks",
                "stripped log side grain",
                "timber boards and construction wood",
                "hardwood plank texture",
                "tree bark surface",
                "wooden crate or barrel staves"
            ]
        },
        new TagRule
        {
            Id = "metal",
            DisplayName = "Metal",
            Kind = TagRuleKind.Material,
            Keywords = ["metal", "gold", "iron", "copper", "ingot", "chain", "netherite", "nugget"],
            SemanticHints = ["metal surface", "shiny ore", "iron ingot", "gold block", "chain link"]
        },
        new TagRule
        {
            Id = "gem",
            DisplayName = "Gem",
            Kind = TagRuleKind.Material,
            Keywords = ["diamond", "emerald", "redstone", "ruby", "sapphire", "amethyst", "topaz"],
            SemanticHints =
            [
                "diamond gemstone block",
                "emerald gemstone crystal",
                "redstone crystal dust",
                "precious gem crystal mineral"
            ]
        },
        new TagRule
        {
            Id = "organic",
            DisplayName = "Organic",
            Kind = TagRuleKind.Material,
            Keywords =
            [
                "leaves", "leaf", "grass", "vine", "fern", "flower", "bush", "sapling", "stem", "kelp", "lily", "mushroom",
                "plant", "coral",
                "allay", "armadillo", "axolotl", "bat", "bee", "blaze", "bogged", "breeze", "camel", "cat", "cave_spider",
                "chicken", "cod", "cow", "creeper", "dolphin", "donkey", "drowned", "elder_guardian", "enderman", "endermite",
                "evoker", "fox", "frog", "ghast", "glow_squid", "goat", "guardian", "hoglin", "horse", "husk", "illusioner",
                "llama", "magma_cube", "mooshroom", "mule", "ocelot", "panda", "parrot", "phantom", "pig", "piglin",
                "piglin_brute", "pillager", "polar_bear", "pufferfish", "rabbit", "ravager", "salmon", "sheep", "shulker",
                "silverfish", "skeleton", "skeleton_horse", "slime", "sniffer", "spider", "squid", "stray", "strider",
                "tadpole", "trader_llama", "tropical_fish", "turtle", "vex", "villager", "vindicator", "wandering_trader",
                "warden", "witch", "wither", "wither_skeleton", "wolf", "zoglin", "zombie", "zombie_horse", "zombie_villager"
            ],
            SemanticHints =
            [
                "seagrass and kelp underwater plant",
                "coral fan or brain coral",
                "short grass and tall grass block",
                "large fern and flower petals",
                "oak or birch leaf canopy",
                "climbing vine plant",
                "lily pad floating plant",
                "wheat or crop field vegetation",
                "moss carpet plant block",
                "villager robe cloth and skin",
                "zombie and husk mob skin",
                "skeleton and wither skeleton bones",
                "creeper face and body texture",
                "spider and cave spider body",
                "piglin and hoglin nether mob",
                "guardian and elder guardian scales",
                "frog and tadpole amphibian",
                "wolf and fox fur texture"
            ]
        },
        new TagRule
        {
            Id = "glass",
            DisplayName = "Glass",
            Kind = TagRuleKind.Material,
            Keywords = ["glass", "pane"],
            SemanticHints = ["transparent glass", "window glass", "stained glass", "glass pane"]
        },
        new TagRule
        {
            Id = "stone",
            DisplayName = "Stone",
            Kind = TagRuleKind.Material,
            Keywords =
            [
                "stone", "cobble", "deepslate", "granite", "andesite", "diorite", "basalt", "tuff", "slate", "marble",
                "rock"
            ],
            SemanticHints =
            [
                "rough stone", "cobblestone", "natural rock", "polished stone", "chiseled stone", "cracked stone",
                "granite rock", "deepslate rock"
            ]
        },
        new TagRule
        {
            Id = "unknown",
            DisplayName = "Unknown",
            Kind = TagRuleKind.Material,
            Keywords = [],
            SemanticHints = []
        }
    ];

    /// <summary>Built-in path/keyword flags (location, armor, UV-related paths).</summary>
    public static IReadOnlyList<TagRule> DefaultFlags { get; } =
    [
        new TagRule
        {
            Id = FlagTagResolver.BlockId,
            DisplayName = "Block",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.ItemId,
            DisplayName = "Item",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.EntityId,
            DisplayName = "Entity",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.ArmorId,
            DisplayName = "Armor",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.UvWrapId,
            DisplayName = "UV wrap",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.OreId,
            DisplayName = "Ore",
            Kind = TagRuleKind.Flag,
            Keywords = ["ore"],
            KeywordsMatchWholeWord = true,
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.WeightedId,
            DisplayName = "Weighted",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.UnweightedId,
            DisplayName = "Unweighted",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        },
        new TagRule
        {
            Id = FlagTagResolver.Sprite2DId,
            DisplayName = "2D Sprite",
            Kind = TagRuleKind.Flag,
            Keywords = [],
            SemanticHints = []
        }
    ];

    /// <summary>All built-in rules: materials first, then flags.</summary>
    public static IReadOnlyList<TagRule> Default { get; } = DefaultMaterials.Concat(DefaultFlags).ToList();

    /// <summary>Built-in expression rules evaluated after automatic material/flag discovery.</summary>
    public static IReadOnlyList<RuleExpressionDefinition> BuiltInExpressions { get; } =
    [
        new RuleExpressionDefinition
        {
            Id = "builtin_no_sprite_for_entity_uv",
            DisplayName = "No 2D Sprite for Entity UV Organic",
            IsBuiltIn = true,
            Priority = 100,
            Condition = new RuleConditionNode
            {
                Type = RuleConditionNodeType.All,
                Children =
                [
                    new RuleConditionNode
                    {
                        Type = RuleConditionNodeType.Any,
                        Children =
                        [
                            new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = "organic" },
                            new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = "plant" }
                        ]
                    },
                    new RuleConditionNode { Type = RuleConditionNodeType.HasFlag, Value = FlagTagResolver.EntityId },
                    new RuleConditionNode { Type = RuleConditionNodeType.HasFlag, Value = FlagTagResolver.UvWrapId }
                ]
            },
            Actions = [new RuleActionDefinition { Type = RuleActionType.RemoveFlag, Value = FlagTagResolver.Sprite2DId }]
        },
        new RuleExpressionDefinition
        {
            Id = "builtin_remove_block_for_non_block_name",
            DisplayName = "Remove Block for Organic Non-Block Name",
            IsBuiltIn = true,
            Priority = 80,
            Condition = new RuleConditionNode
            {
                Type = RuleConditionNodeType.All,
                Children =
                [
                    new RuleConditionNode
                    {
                        Type = RuleConditionNodeType.Any,
                        Children =
                        [
                            new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = "organic" },
                            new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = "plant" }
                        ]
                    },
                    new RuleConditionNode { Type = RuleConditionNodeType.HasFlag, Value = FlagTagResolver.BlockId },
                    new RuleConditionNode
                    {
                        Type = RuleConditionNodeType.Not,
                        Children = [new RuleConditionNode { Type = RuleConditionNodeType.NameContains, Value = "block" }]
                    }
                ]
            },
            Actions = [new RuleActionDefinition { Type = RuleActionType.RemoveFlag, Value = FlagTagResolver.BlockId }]
        }
    ];

    /// <summary>Returns material tag ids whose keywords match the texture name or relative key (case-insensitive).</summary>
    public static IReadOnlyList<string> GetMatchingMaterialTagIds(string name, string relativeKey, IReadOnlyList<TagRule> rules) =>
        TagRuleApplicator.GetMatchingTagIds(name, relativeKey, rules, TagRuleKind.Material);
}
