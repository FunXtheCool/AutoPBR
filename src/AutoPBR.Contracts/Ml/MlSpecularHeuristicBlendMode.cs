namespace AutoPBR.Contracts.Ml;

/// <summary>How ML specular is mixed with heuristic output when both are available.</summary>
public enum MlSpecularHeuristicBlendMode
{
    /// <summary>
    /// Only smoothness (R) mixes heuristic vs ML. Metallic (G), porosity (B), and emissive (A) use the model only.
    /// </summary>
    SmoothnessOnly = 0,

    /// <summary>Heuristic contributes to every channel: R, G, B, and A each lerp between heuristic and ML.</summary>
    Full = 1,

    /// <summary>
    /// Same as <see cref="SmoothnessOnly"/> for R/G/A: only R blends heuristic↔ML while G/A come from ML.
    /// B (porosity) stays heuristic.
    /// </summary>
    AiMetalAndEmissive = 2
}
