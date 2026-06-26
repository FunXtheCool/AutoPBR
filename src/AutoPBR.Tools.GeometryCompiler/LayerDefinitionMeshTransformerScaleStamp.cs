using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Lifts a terminal <c>LayerDefinition.apply(MeshTransformer.scaling(f))</c> into the synthetic root pose.
/// Vanilla <c>MeshTransformer.scaling</c> scales the root pose and translates it by
/// <c>24.016f * (1 - f)</c> on Y.
/// </summary>
internal static class LayerDefinitionMeshTransformerScaleStamp
{
    private const double ScalingPivotY = 24.016;

    public static bool ApplyToLiftedRoots(JsonArray roots, string meshConcat)
    {
        if (!TryReadAppliedScale(meshConcat, out var scale) ||
            roots.Count == 0 ||
            roots[0] is not JsonObject root)
        {
            return false;
        }

        var pose = root["pose"] as JsonObject ?? new JsonObject();
        root["pose"] = pose;

        var translation = pose["translation"] as JsonArray;
        var x = ReadCoordinate(translation, 0) * scale;
        var y = ReadCoordinate(translation, 1) * scale + ScalingPivotY * (1d - scale);
        var z = ReadCoordinate(translation, 2) * scale;
        pose["translation"] = new JsonArray(
            GeometryLiftCoordinateRounding.Round(x),
            GeometryLiftCoordinateRounding.Round(y),
            GeometryLiftCoordinateRounding.Round(z));

        var existingScale = pose["uniformScale"]?.GetValue<double>() ?? 1d;
        pose["uniformScale"] = GeometryLiftCoordinateRounding.Round(existingScale * scale);
        pose["rotationEulerRad"] ??= new JsonArray(0d, 0d, 0d);
        pose["eulerOrder"] ??= "XYZ";
        return true;
    }

    private static bool TryReadAppliedScale(string meshConcat, out double scale)
    {
        scale = 1d;
        var lines = JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(
            meshConcat.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .ToList());

        var found = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("MeshTransformer.scaling:(F)", StringComparison.Ordinal))
            {
                continue;
            }

            var hasLayerApply = false;
            for (var j = i + 1; j < lines.Count && j <= i + 8; j++)
            {
                if (lines[j].Contains(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringComparison.Ordinal))
                {
                    break;
                }

                if (lines[j].Contains("LayerDefinition.apply:", StringComparison.Ordinal))
                {
                    hasLayerApply = true;
                    break;
                }
            }

            if (!hasLayerApply)
            {
                continue;
            }

            for (var j = i - 1; j >= 0 && j >= i - 6; j--)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[j], out var parsed))
                {
                    continue;
                }

                scale = parsed;
                found = true;
                break;
            }
        }

        return found && double.IsFinite(scale) && scale > 0d;
    }

    private static double ReadCoordinate(JsonArray? translation, int index) =>
        translation is not null && translation.Count > index
            ? translation[index]?.GetValue<double>() ?? 0d
            : 0d;
}
