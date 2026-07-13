using System.IO.Hashing;

namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>Stable content identity for preview materials (texture uploads and item voxel meshes).</summary>
public static class PreviewMaterialContentKey
{
    public readonly record struct Value(
        int Width,
        int Height,
        ulong AlbedoFp,
        ulong NormalFp,
        ulong SpecularFp,
        ulong HeightFp,
        bool IsPlantForNoHeight,
        bool Sprite2DFoliageTarget);

    public static Value Compute(PreviewMaterial? material)
    {
        if (material is null)
        {
            return default;
        }

        return new Value(
            material.Width,
            material.Height,
            Fingerprint(material.AlbedoRgba.Span),
            FingerprintOrEmpty(material.NormalRgba),
            FingerprintOrEmpty(material.SpecularRgba),
            FingerprintOrEmpty(material.HeightRgba),
            material.IsPlantForNoHeight,
            material.Sprite2DFoliageTarget);
    }

    private static ulong FingerprintOrEmpty(ReadOnlyMemory<byte>? rgba) =>
        rgba is { Length: > 0 } mem ? Fingerprint(mem.Span) : 0;

    public static bool Equals(in Value a, in Value b) =>
        a.Width == b.Width &&
        a.Height == b.Height &&
        a.AlbedoFp == b.AlbedoFp &&
        a.NormalFp == b.NormalFp &&
        a.SpecularFp == b.SpecularFp &&
        a.HeightFp == b.HeightFp &&
        a.IsPlantForNoHeight == b.IsPlantForNoHeight &&
        a.Sprite2DFoliageTarget == b.Sprite2DFoliageTarget;

    private static ulong Fingerprint(ReadOnlySpan<byte> rgba)
    {
        if (rgba.IsEmpty)
        {
            return 0;
        }

        var hash = new XxHash64();
        hash.Append(rgba);
        return hash.GetCurrentHashAsUInt64();
    }
}
