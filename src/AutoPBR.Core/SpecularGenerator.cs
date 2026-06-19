using AutoPBR.Core.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

/// <summary>
/// Generates LabPBR-compatible specular (_s) textures from diffuse inputs.
/// </summary>
internal static partial class SpecularGenerator
{
    /// <summary>LabPBR: G channel &lt;= this value is F0 (dielectric); 230+ = metal.</summary>
    private const byte LabPbrF0CapDielectric = 229;
    private const int VcOrientationCount = 12;
    private static readonly float[] VcCos = BuildVcCos();
    private static readonly float[] VcSin = BuildVcSin();

    private static string? BuildSpecularModelLogLine(
        string textureName,
        AutoPbrOptions options,
        bool useMlSpecular,
        string? mlDiagnostic)
    {
        if (!options.UseMlSpecularPredictor)
        {
            return null;
        }

        string? core;
        if (!useMlSpecular)
        {
            core = $"[{textureName}] Specular: heuristic/tag path only (ML specular did not produce a tensor — see diagnostic below if present).";
        }
        else
        {
            var blend = Math.Clamp(options.MlSpecularHeuristicBlend, 0f, 1f);
            var mode = options.MlSpecularHeuristicBlendMode;
            core = $"[{textureName}] Specular: ML+heuristic mix (blend {blend:0.##}, mode {mode}).";
        }

        var parts = new List<string> { core };
        if (options.SpecularDebugDisableHeuristicSpecular)
        {
            parts.Add("debugNoHeuristic=magenta fallback");
        }

        if (options.SpecularDebugSkipSpecularRemap)
        {
            parts.Add("debugSkipRRemap");
        }

        if (!string.IsNullOrWhiteSpace(mlDiagnostic))
        {
            parts.Add(mlDiagnostic);
        }

        return string.Join(" | ", parts);
    }

    private sealed class SpecularTileResult
    {
        public required bool HasData { get; init; }
        public required bool UseMlSpec { get; init; }
        public required string? MlDiagnostic { get; init; }
        public Image<Rgba32>? Image { get; init; }
    }
}
