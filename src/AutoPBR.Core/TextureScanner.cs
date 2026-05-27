using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    private sealed record ScanCandidate(
        string File,
        string Name,
        string Extension,
        string DirectoryPath,
        string RelativePathNoExt,
        bool SpecularOnly);

    private sealed record TagComputationResult(
        bool Sprite2DFoliageTarget,
        bool HasPlantMaterialTag,
        bool HasBrickMaterialTag,
        bool HasUvWrap,
        bool InvertSpecular,
        bool InvertHeight,
        IReadOnlyList<string> EffectiveTagIds);
}
