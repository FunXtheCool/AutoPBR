using System.Globalization;
using System.Text;
using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

/// <summary>Formats draw-batch layer policy for Explore 3D diagnostics.</summary>
public static class PreviewDrawBatchDiagnostics
{
    public static string FormatBatchSummary(PreviewDrawBatch[]? batches, int materialCount)
    {
        if (batches is not { Length: > 0 })
        {
            return $"materials={materialCount} batches=0";
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"materials={materialCount} batches={batches.Length}");
        for (var i = 0; i < batches.Length; i++)
        {
            var b = batches[i];
            var p = b.LayerPolicy;
            sb.Append(
                CultureInfo.InvariantCulture,
                $"\n  [{i}] mat={b.MaterialIndex} kind={p.Kind} order={p.DrawOrder} bias={p.DepthBiasStep} " +
                $"depthWrite={p.DepthWrite} shadow={p.ShadowMode} pom={(b.EnableParallax ? "On" : "Off")} " +
                $"idx={b.FirstIndex}+{b.IndexCount}");
        }

        return sb.ToString();
    }
}
