using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class MaterialTagMlPostProcessorTests
{
    private static readonly IReadOnlyList<TagRule> MaterialRules = TagRulePresets.DefaultMaterials;

    [Fact]
    public void ApplyOrePathKeepsMetalAndAddsStone()
    {
        var rules = MaterialRules;
        var result = MaterialTagMlPostProcessor.Apply(
            "iron_ore",
            @"\minecraft\textures\block\iron_ore",
            ["metal", "unknown"],
            rules,
            maxMaterialTags: null);

        Assert.Contains("stone", result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("metal", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyNonOrePathKeepsMetal()
    {
        var rules = MaterialRules;
        var result = MaterialTagMlPostProcessor.Apply(
            "iron_ingot",
            @"\minecraft\textures\item\iron_ingot",
            ["metal"],
            rules,
            maxMaterialTags: null);

        Assert.Contains("metal", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyForestsNameDoesNotTriggerOreStoneHeuristic()
    {
        var result = MaterialTagMlPostProcessor.Apply(
            "forests",
            @"\minecraft\textures\block\forests",
            ["metal", "unknown"],
            MaterialRules,
            maxMaterialTags: null);

        Assert.Contains("metal", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyOrePathGemAndStoneLowMaxTagKeepsBoth()
    {
        var result = MaterialTagMlPostProcessor.Apply(
            "diamond_ore",
            @"\minecraft\textures\block\diamond_ore",
            ["gem", "unknown"],
            MaterialRules,
            maxMaterialTags: 1);

        Assert.Contains("stone", result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("gem", result, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyOrePathGemStoneAndExtrasPrioritizesStoneGemWhenTrimming()
    {
        var result = MaterialTagMlPostProcessor.Apply(
            "diamond_ore",
            @"\minecraft\textures\block\diamond_ore",
            ["gem", "brick", "wood", "glass"],
            MaterialRules,
            maxMaterialTags: 2);

        Assert.Contains("stone", result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("gem", result, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ApplyOrePathMetalResolvedLowMaxTagKeepsStoneAndMetal()
    {
        var result = MaterialTagMlPostProcessor.Apply(
            "iron_ore",
            @"\minecraft\textures\block\iron_ore",
            ["metal", "brick", "glass"],
            MaterialRules,
            maxMaterialTags: 1);

        Assert.Contains("stone", result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("metal", result, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyOrePathCoalDoesNotApplyCapPairing()
    {
        var result = MaterialTagMlPostProcessor.Apply(
            "deepslate_coal_ore",
            @"\minecraft\textures\block\deepslate_coal_ore",
            ["gem", "unknown"],
            MaterialRules,
            maxMaterialTags: 1);

        Assert.Contains("stone", result, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("gem", result, StringComparer.OrdinalIgnoreCase);
        Assert.Single(result);
    }
}
