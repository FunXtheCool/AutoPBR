using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Infers <c>faceMask</c> for zero-thickness lifted cuboids when bytecode omits a direction <c>Set</c>.
/// Shares axis→face rules with <see cref="GeometryIrUvAtlasQuality.InferDegenerateFaceMask"/> (preview emit fallback).
/// </summary>
internal static class GeometryLiftDegenerateFaceMask
{
    internal static List<string>? ApplyForLift(
        List<string>? faceMaskFromBytecode,
        double sizeX,
        double sizeY,
        double sizeZ)
    {
        var w = (int)Math.Round(Math.Abs(sizeX));
        var h = (int)Math.Round(Math.Abs(sizeY));
        var d = (int)Math.Round(Math.Abs(sizeZ));

        if (faceMaskFromBytecode is { Count: > 0 })
        {
            var sanitized = GeometryIrUvAtlasQuality.SanitizeFaceMaskForLogicalExtents(
                w,
                h,
                d,
                faceMaskFromBytecode.ToArray());
            return sanitized?.ToList();
        }

        if (faceMaskFromBytecode is not null)
        {
            return faceMaskFromBytecode;
        }

        var inferred = GeometryIrUvAtlasQuality.InferDegenerateFaceMask(w, h, d);
        return inferred is { Length: > 0 } ? inferred.ToList() : null;
    }
}
