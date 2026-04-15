using AutoPBR.Core.Embeddings;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class DictionarySemanticTests
{
    [Fact]
    public void ParseDefinitions_ExtractsSenseDefinitions()
    {
        const string json = """
        {
          "word": "granite",
          "entries": [
            {
              "language": { "code": "en", "name": "English" },
              "senses": [
                { "definition": "A hard granular crystalline rock." },
                { "definition": "A type of stone used in construction." }
              ]
            }
          ]
        }
        """;

        var defs = FreeDictionaryDefinitionProvider.ParseDefinitions(json);

        Assert.Equal(2, defs.Count);
        Assert.Contains("A hard granular crystalline rock.", defs);
        Assert.Contains("A type of stone used in construction.", defs);
    }

    [Fact]
    public void ParseDefinitions_InvalidJson_ReturnsEmpty()
    {
        var defs = FreeDictionaryDefinitionProvider.ParseDefinitions("{not-json}");
        Assert.Empty(defs);
    }

    [Fact]
    public void ExtractTerms_RemovesDirectionalFlippedAndNumericTokens()
    {
        var terms = MaterialTagSemanticQuery.ExtractTerms("stone_top_flipped_01 side sides granite");

        Assert.Contains("stone", terms);
        Assert.Contains("granite", terms);
        Assert.DoesNotContain("top", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("side", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("sides", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("flipped", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("01", terms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractTerms_RemovesBridgingStopWords()
    {
        var terms = MaterialTagSemanticQuery.ExtractTerms("the_and_or_but_granite_of_in_for");

        Assert.Contains("granite", terms);
        Assert.DoesNotContain("the", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("and", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("or", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("but", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("of", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("in", terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("for", terms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractOrderedDictionaryTerms_MinLengthThree_MaxEight_FirstOccurrenceOrder()
    {
        var ordered = MaterialTagSemanticQuery.ExtractOrderedDictionaryTerms("oak_door_iron_trapdoor_extra");
        Assert.Equal(new[] { "oak", "door", "iron", "trapdoor", "extra" }, ordered);
    }
}
