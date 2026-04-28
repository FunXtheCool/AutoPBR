using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class FlagTagResolverTests
{
    [Fact]
    public void Resolve_AddsUvWrapFromAtlasGeometryEvidence()
    {
        var ids = FlagTagResolver.Resolve(
            textureName: "custom_tile",
            relativeKey: @"\minecraft\textures\custom\my_tile",
            flagRules: [],
            context: new FlagTagResolver.ResolveContext(
                ExplicitUvWrap: null,
                TextureWidth: 64,
                TextureHeight: 128));

        Assert.Contains(FlagTagResolver.UvWrapId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RespectsExplicitDisableEvenWithGeometryEvidence()
    {
        var ids = FlagTagResolver.Resolve(
            textureName: "custom_tile",
            relativeKey: @"\minecraft\textures\custom\my_tile",
            flagRules: [],
            context: new FlagTagResolver.ResolveContext(
                ExplicitUvWrap: false,
                TextureWidth: 64,
                TextureHeight: 128));

        Assert.DoesNotContain(FlagTagResolver.UvWrapId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RespectsNoUvWrapKeywordOverride()
    {
        var noUvRule = new TagRule
        {
            Id = FlagTagResolver.NoUvWrapId,
            DisplayName = "No UV Wrap",
            Kind = TagRuleKind.Flag,
            Keywords = ["tile"],
            KeywordsMatchWholeWord = false
        };

        var ids = FlagTagResolver.Resolve(
            textureName: "tile_texture",
            relativeKey: @"\minecraft\textures\entity\tile_texture",
            flagRules: [noUvRule]);

        Assert.DoesNotContain(FlagTagResolver.UvWrapId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_DoesNotInferUvWrapFromSquareGeometry()
    {
        var ids = FlagTagResolver.Resolve(
            textureName: "stone",
            relativeKey: @"\minecraft\textures\block\stone",
            flagRules: [],
            context: new FlagTagResolver.ResolveContext(
                ExplicitUvWrap: null,
                TextureWidth: 32,
                TextureHeight: 32));

        Assert.DoesNotContain(FlagTagResolver.UvWrapId, ids, StringComparer.OrdinalIgnoreCase);
    }
}
