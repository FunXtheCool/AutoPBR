using System.Text.RegularExpressions;
using AutoPBR.Contracts.Ml;
using AutoPBR.Contracts;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    /// <summary>Multiplier on dictionary evidence from lookups of <c>door</c> / <c>trapdoor</c> (generic glosses dilute material).</summary>
    private const float DoorTrapdoorDictionaryPenalty = 0.35f;

    /// <summary>Boost for the term before <c>door</c> / <c>trapdoor</c> (e.g. wood/iron in the file name).</summary>
    private const float DoorPrecedingTermDictionaryBoost = 1.45f;

    /// <summary>Extra dictionary score for <c>wood</c> when the query includes door/trapdoor (Minecraft default).</summary>
    private const float WoodDictionaryBiasWhenDoorOrTrapdoor = 0.10f;

    private readonly MiniLmEmbeddingEngine _engine;
    private readonly Dictionary<string, float[]> _prototypeCache = new(StringComparer.OrdinalIgnoreCase);

    private MaterialTagSemanticMatcher(MiniLmEmbeddingEngine engine) => _engine = engine;
    public static MaterialTagSemanticMatcher? TryCreate(string? baseDirectory = null)
    {
        var engine = MiniLmEmbeddingEngine.TryCreate(baseDirectory);
        return engine is null ? null : new MaterialTagSemanticMatcher(engine);
    }
    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    public void Dispose() => _engine.Dispose();
}
