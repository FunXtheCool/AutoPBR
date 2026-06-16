using System.Text.Json;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Assigns preview depth layer intent from geometry IR cuboids, part ids, and JVM context.
/// </summary>
internal static class PreviewDepthLayerClassifier
{
    public static (PreviewDepthLayerKind Kind, int LayerOrdinal, bool CastsShadow) ClassifyIrCuboid(
        string partId,
        string? officialJvmName,
        JsonElement cuboid,
        int cuboidIndexOnPart,
        int cuboidCountOnPart) =>
        PreviewDepthLayerResolver.ClassifyIrCuboid(
            partId,
            officialJvmName,
            cuboid,
            cuboidIndexOnPart,
            cuboidCountOnPart);
}
