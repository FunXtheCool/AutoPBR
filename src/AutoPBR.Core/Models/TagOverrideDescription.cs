namespace AutoPBR.Core.Models;

/// <summary>Human-readable summary of <see cref="TextureOverrides"/> for tag legends and tooltips.</summary>
public static class TagOverrideDescription
{
    public static string Summarize(TextureOverrides o)
    {
        var parts = new List<string>();
        if (o.InvertHeight)
        {
            parts.Add("Invert height");
        }

        if (o.InvertSpecular)
        {
            parts.Add("Invert specular");
        }

        if (o.InvertNormalRed)
        {
            parts.Add("Invert normal X (red)");
        }

        if (o.InvertNormalGreen)
        {
            parts.Add("Invert normal Y (green)");
        }

        if (o.NormalIntensity is { } ni)
        {
            parts.Add($"Normal intensity ×{ni:0.###}");
        }

        if (o.HeightIntensity is { } hi)
        {
            parts.Add($"Height intensity ×{hi:0.###}");
        }

        if (o.HeightBrightness is { } hb)
        {
            parts.Add($"Height brightness ×{hb:0.###}");
        }

        if (o.FastSpecular == true)
        {
            parts.Add("Fast specular");
        }
        else if (o.FastSpecular == false)
        {
            parts.Add("Full specular");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "Keyword match only (no extra overrides)";
    }
}
