using AutoPBR.Core.Models;

namespace AutoPBR.Core;

public static class AutoPbrDefaults
{
    public const float DefaultNormalIntensity = 1f;
    public const float DefaultHeightIntensity = 0.12f;
    public const float DefaultHeightBrightness = 1f;
    public const float DefaultSmoothnessScale = 1f;
    public const float DefaultMetallicBoost = 1f;
    public const int DefaultPorosityBias = 0;

    /// <summary>Extra B-channel offset for organic-tagged textures (added to <see cref="AutoPbrOptions.PorosityBias"/>).</summary>
    public const int DefaultPlantMaterialPorosityExtra = 48;

    /// <summary>See <see cref="AutoPbrOptions.MlSpecularHeuristicBlend"/>.</summary>
    public const float DefaultMlSpecularHeuristicBlend = 1f;

    /// <summary>See <see cref="AutoPbrOptions.MlSpecularHeuristicBlendMode"/>.</summary>
    public const MlSpecularHeuristicBlendMode DefaultMlSpecularHeuristicBlendMode = MlSpecularHeuristicBlendMode.SmoothnessOnly;

    /// <summary>See <see cref="AutoPbrOptions.MlSpecularBlendMath"/>.</summary>
    public const MlSpecularBlendMath DefaultMlSpecularBlendMath = MlSpecularBlendMath.Linear;

    /// <summary>See <see cref="AutoPbrOptions.BrickHeightMapPostProcessEnabled"/>.</summary>
    public const bool DefaultBrickHeightMapPostProcessEnabled = true;

    /// <summary>Combined with the invert confidence floor as the minimum mean structural response for the strong invert path (large height delta).</summary>
    public const float DefaultBrickHeightMinStructuralConfidence = 0.012f;

    /// <summary>Minimum mean mortar response for the strong Δ global invert path (thin joints often stay below this).</summary>
    public const float DefaultBrickHeightInvertConfidenceFloor = 0.038f;

    /// <summary>Mortar minus brick mean height (0–255) above this may trigger global invert on the strong path.</summary>
    public const float DefaultBrickHeightInvertDeltaThreshold = 2f;

    /// <summary>
    /// Weighted diffuse luminance on mortar minus brick (0–1); above this enables the light-grout invert path when Δ&gt;0.
    /// </summary>
    public const float DefaultBrickLightGroutDiffuseDeltaMin = 0.004f;

    /// <summary>Strength of local depression along detected mortar (0–1).</summary>
    public const float DefaultBrickMortarDepressionAlpha = 0.42f;

    /// <summary>Max lift added on bulk brick regions (0–255 scale) via brick-face mask.</summary>
    public const float DefaultBrickBulkLiftBeta = 10f;

    /// <summary>
    /// Upper bound for top-hat radii; <see cref="BrickProbeResolution.GetTopHatRadii"/> also applies a resolution-based
    /// floor (max(user, minDim/16 clamped)) so 64²–256² joints are detected when this default is left in place.
    /// </summary>
    public const int DefaultBrickMortarTopHatMaxRadius = 3;

    /// <summary>See <see cref="AutoPbrOptions.BrickSpecularAlignWithHeightProbe"/>.</summary>
    public const bool DefaultBrickSpecularAlignWithHeightProbe = true;

    /// <summary>When true, preview captures a brick probe diagnostics string on the work item (UI / debugging).</summary>
    public const bool DefaultBrickProbePreviewDebug = false;

    // Matches upstream Python `excluded_names` list (by filename).
    public static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "blast_furnace_front_on.png",
        "campfire_fire.png",
        "campfire_log_lit.png",
        "chain_command_block_back.png",
        "chain_command_block_conditional.png",
        "chain_command_block_front.png",
        "chain_command_block_side.png",
        "command_block_back.png",
        "command_block_conditional.png",
        "command_block_front.png",
        "command_block_side.png",
        "crimson_stem.png",
        "fire_0.png",
        "fire_1.png",
        "kelp.png",
        "kelp_plant.png",
        "lava_flow.png",
        "lava_still.png",
        "nether_portal.png",
        "pumpkin_face_on.png",
        "redstone_lamp_on.png",
        "repeating_command_block_back.png",
        "repeating_command_block_conditional.png",
        "repeating_command_block_front.png",
        "repeating_command_block_side.png",
        "respawn_anchor_top.png",
        "seagrass.png",
        "smoker_front_on.png",
        "soul_campfire_fire.png",
        "soul_campfire_log_lit.png",
        "soul_fire_0.png",
        "soul_fire_1.png",
        "stonecutter_saw.png",
        "tall_seagrass_bottom.png",
        "tall_seagrass_top.png",
        "warped_stem.png",
        "water_flow.png",
        "water_still.png",
    };

    /// <summary>Known vanilla block texture keys (namespace-prefixed) used when the organic material tag is absent.</summary>
    public static readonly HashSet<string> PlantTextureKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "\\minecraft\\block\\acacia_sapling",
        "\\minecraft\\block\\birch_sapling",
        "\\minecraft\\block\\dark_oak_sapling",
        "\\minecraft\\block\\jungle_sapling",
        "\\minecraft\\block\\oak_sapling",
        "\\minecraft\\block\\spruce_sapling",
        "\\minecraft\\block\\allium",
        "\\minecraft\\block\\azure_bluet",
        "\\minecraft\\block\\blue_orchid",
        "\\minecraft\\block\\cornflower",
        "\\minecraft\\block\\dandelion",
        "\\minecraft\\block\\lilac_top",
        "\\minecraft\\block\\lilac_bottom",
        "\\minecraft\\block\\lily_of_the_valley",
        "\\minecraft\\block\\orange_tulip",
        "\\minecraft\\block\\oxeye_daisy",
        "\\minecraft\\block\\peony_top",
        "\\minecraft\\block\\peony_bottom",
        "\\minecraft\\block\\pink_tulip",
        "\\minecraft\\block\\poppy",
        "\\minecraft\\block\\red_tulip",
        "\\minecraft\\block\\rose_bush_top",
        "\\minecraft\\block\\rose_bush_bottom",
        "\\minecraft\\block\\sunflower_back",
        "\\minecraft\\block\\sunflower_front",
        "\\minecraft\\block\\sunflower_bottom",
        "\\minecraft\\block\\sunflower_top",
        "\\minecraft\\block\\white_tulip",
        "\\minecraft\\block\\wither_rose",
        "\\minecraft\\block\\brown_mushroom",
        "\\minecraft\\block\\red_mushroom",
        "\\minecraft\\block\\hanging_roots",
        "\\minecraft\\block\\spore_blossom",
        "\\minecraft\\block\\small_dripleaf_top_extra",
        "\\minecraft\\block\\small_dripleaf_top",
        "\\minecraft\\block\\small_dripleaf_stem_top",
        "\\minecraft\\block\\small_dripleaf_stem_bottom",
        "\\minecraft\\block\\big_dripleaf_stem",
        "\\minecraft\\block\\big_dripleaf_tip",
        "\\minecraft\\block\\big_dripleaf_top",
        "\\minecraft\\block\\big_dripleaf_tip_extra",
        "\\minecraft\\block\\big_dripleaf_top_extra",
        "\\minecraft\\block\\dead_bush",
        "\\minecraft\\block\\fern",
        "\\minecraft\\block\\large_fern_bottom",
        "\\minecraft\\block\\large_fern_top",
        "\\minecraft\\block\\short_grass",
        "\\minecraft\\block\\tall_grass_bottom",
        "\\minecraft\\block\\tall_grass_top",
        "\\minecraft\\block\\tall_seagrass_bottom",
        "\\minecraft\\block\\tall_seagrass_top",
        "\\minecraft\\block\\beetroots_stage0",
        "\\minecraft\\block\\beetroots_stage1",
        "\\minecraft\\block\\beetroots_stage2",
        "\\minecraft\\block\\beetroots_stage3",
        "\\minecraft\\block\\carrots_stage0",
        "\\minecraft\\block\\carrots_stage1",
        "\\minecraft\\block\\carrots_stage2",
        "\\minecraft\\block\\carrots_stage3",
        "\\minecraft\\block\\potatoes_stage0",
        "\\minecraft\\block\\potatoes_stage1",
        "\\minecraft\\block\\potatoes_stage2",
        "\\minecraft\\block\\potatoes_stage3",
        "\\minecraft\\block\\wheat",
        "\\minecraft\\block\\sweet_berries",
        "\\minecraft\\block\\nether_wart_stage0",
        "\\minecraft\\block\\nether_wart_stage1",
        "\\minecraft\\block\\nether_wart_stage2",
        "\\minecraft\\block\\cocoa_stage0",
        "\\minecraft\\block\\cocoa_stage1",
        "\\minecraft\\block\\cocoa_stage2",
    };
}
