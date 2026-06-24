namespace AutoPBR.Core.Models;

using System.Numerics;

/// <summary>Depth and draw-order intent for a preview mesh layer (base body vs overlay shells).</summary>
public enum PreviewDepthLayerKind
{
    Base = 0,
    CutoutOverlay = 1,
    CosmeticOverlay = 2,
    EmissiveOverlay = 3,
    DebugOnly = 4,

    /// <summary>Alpha-blended shell (slime outer body via vanilla <c>entityTranslucent</c>).</summary>
    TranslucentOverlay = 5,
}

/// <summary>Whether a draw batch participates in shadow-map depth passes.</summary>
public enum PreviewDrawLayerShadowMode
{
    /// <summary>Draw into shadow maps (base and physical shells).</summary>
    Draw = 0,

    /// <summary>Skip shadow pass (cosmetic overlays, eyes, profession paint).</summary>
    Skip = 1,
}

/// <summary>
/// Per-batch depth and ordering policy for Explore 3D preview draws. Defaults match ordinary opaque geometry.
/// </summary>
public readonly struct PreviewDrawLayerPolicy() : IEquatable<PreviewDrawLayerPolicy>
{
    public PreviewDepthLayerKind Kind { get; init; } = PreviewDepthLayerKind.Base;
    public int DrawOrder { get; init; } = 0;
    public int DepthBiasStep { get; init; } = 0;
    public bool DepthWrite { get; init; } = true;
    public PreviewDrawLayerShadowMode ShadowMode { get; init; } = PreviewDrawLayerShadowMode.Draw;

    public static PreviewDrawLayerPolicy DefaultBase { get; } = ForKind(PreviewDepthLayerKind.Base);

    /// <summary>False-color tint for <see cref="EntityPreviewDebugSettings.ShowDepthLayerDebug"/>.</summary>
    public static Vector3 GetDebugTint(PreviewDepthLayerKind kind) =>
        kind switch
        {
            PreviewDepthLayerKind.CutoutOverlay => new Vector3(0.2f, 0.85f, 0.95f),
            PreviewDepthLayerKind.CosmeticOverlay => new Vector3(0.95f, 0.35f, 0.85f),
            PreviewDepthLayerKind.EmissiveOverlay => new Vector3(0.95f, 0.9f, 0.2f),
            PreviewDepthLayerKind.TranslucentOverlay => new Vector3(0.35f, 0.95f, 0.45f),
            PreviewDepthLayerKind.DebugOnly => new Vector3(0.95f, 0.25f, 0.2f),
            _ => new Vector3(0.75f, 0.75f, 0.75f),
        };

    private const int MaxDepthBiasStep = 8;

    public static PreviewDrawLayerPolicy ForKind(PreviewDepthLayerKind kind, int layerOrdinal = 0)
    {
        var biasStep = Math.Min(MaxDepthBiasStep, 1 + layerOrdinal);
        switch (kind)
        {
            case PreviewDepthLayerKind.CutoutOverlay:
                return new PreviewDrawLayerPolicy
                {
                    Kind = kind,
                    DrawOrder = 100 + Math.Min(layerOrdinal, MaxDepthBiasStep),
                    DepthBiasStep = biasStep,
                    // Write biased depth so post volumetrics (cloud composite depth gate) and later
                    // passes occlude the outer shell; depth-write-off left far-plane depth on overlays.
                    DepthWrite = true,
                    ShadowMode = PreviewDrawLayerShadowMode.Skip,
                };
            case PreviewDepthLayerKind.CosmeticOverlay:
                return new PreviewDrawLayerPolicy
                {
                    Kind = kind,
                    DrawOrder = 200 + Math.Min(layerOrdinal, MaxDepthBiasStep),
                    DepthBiasStep = biasStep,
                    DepthWrite = true,
                    ShadowMode = PreviewDrawLayerShadowMode.Skip,
                };
            case PreviewDepthLayerKind.EmissiveOverlay:
                return new PreviewDrawLayerPolicy
                {
                    Kind = kind,
                    DrawOrder = 300 + layerOrdinal,
                    DepthBiasStep = 2,
                    DepthWrite = true,
                    ShadowMode = PreviewDrawLayerShadowMode.Skip,
                };
            case PreviewDepthLayerKind.TranslucentOverlay:
                return new PreviewDrawLayerPolicy
                {
                    Kind = kind,
                    DrawOrder = 350 + Math.Min(layerOrdinal, MaxDepthBiasStep),
                    DepthBiasStep = 1,
                    DepthWrite = false,
                    ShadowMode = PreviewDrawLayerShadowMode.Skip,
                };
            case PreviewDepthLayerKind.DebugOnly:
                return new PreviewDrawLayerPolicy
                {
                    Kind = kind,
                    DrawOrder = 400 + layerOrdinal,
                    DepthBiasStep = 0,
                    DepthWrite = false,
                    ShadowMode = PreviewDrawLayerShadowMode.Skip,
                };
            default:
                return new PreviewDrawLayerPolicy
                {
                    Kind = PreviewDepthLayerKind.Base,
                    DrawOrder = 0,
                    DepthBiasStep = 0,
                    DepthWrite = true,
                    ShadowMode = PreviewDrawLayerShadowMode.Draw,
                };
        }
    }

    public bool Equals(PreviewDrawLayerPolicy other) =>
        Kind == other.Kind &&
        DrawOrder == other.DrawOrder &&
        DepthBiasStep == other.DepthBiasStep &&
        DepthWrite == other.DepthWrite &&
        ShadowMode == other.ShadowMode;

    public override bool Equals(object? obj) => obj is PreviewDrawLayerPolicy other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, DrawOrder, DepthBiasStep, DepthWrite, ShadowMode);

    public static bool operator ==(PreviewDrawLayerPolicy left, PreviewDrawLayerPolicy right) => left.Equals(right);

    public static bool operator !=(PreviewDrawLayerPolicy left, PreviewDrawLayerPolicy right) => !left.Equals(right);
}

/// <summary>Stable sort order for <see cref="PreviewDrawBatch"/> before GPU upload or draw.</summary>
public static class PreviewDrawBatchOrdering
{
    public static void Sort(List<PreviewDrawBatch> batches)
    {
        batches.Sort(static (a, b) =>
        {
            var cmp = a.LayerPolicy.DrawOrder.CompareTo(b.LayerPolicy.DrawOrder);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.LayerPolicy.Kind.CompareTo(b.LayerPolicy.Kind);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.MaterialIndex.CompareTo(b.MaterialIndex);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.FirstIndex.CompareTo(b.FirstIndex);
        });
    }

    public static PreviewDrawBatch[] Sort(IReadOnlyList<PreviewDrawBatch> batches)
    {
        if (batches.Count == 0)
        {
            return [];
        }

        var list = (batches as List<PreviewDrawBatch>) ?? [.. batches];

        Sort(list);
        return list.ToArray();
    }
}
