namespace AutoPBR.Core;

/// <summary>
/// Resolution-aware morphological scales for the brick mortar height probe (16²–256²+ block textures).
/// Joint thickness and brick-cell size grow with pack resolution; fixed pixel radii do not.
/// </summary>
internal static class BrickProbeResolution
{
    /// <summary>
    /// Pixels to dilate the normalized soft mortar mask before forming brick-face weights (1 − dilate(mortar)).
    /// Uses a tier tied to the shorter image edge: 16²–128² match the historical clamp(minDim/16, 2, 6); 256²+ use a
    /// looser cap so HD joints are not swallowed by an absolute ceiling of 6px.
    /// </summary>
    public static int MortarBrickFaceDilateRadius(int minDim)
    {
        if (minDim < 4)
        {
            return 1;
        }

        if (minDim <= 128)
        {
            return Math.Clamp(minDim / 16, 2, 6);
        }

        // HD / high-res packs: scale with edge length; cap by ~15% of minDim so brick-face mass remains.
        var scaled = Math.Max(2, (int)Math.Ceiling(minDim * 0.0325f));
        var maxByImage = Math.Clamp((minDim * 3 + 19) / 20, 4, 24);
        return Math.Clamp(scaled, 2, maxByImage);
    }

    /// <summary>
    /// Structuring-element radii for multi-scale white/black top-hat (same r for opening and closing).
    /// <paramref name="userMaxRadius"/> is combined with a resolution floor so HD packs still try coarse scales
    /// when the user leaves defaults (see <see cref="AutoPbrDefaults.DefaultBrickMortarTopHatMaxRadius"/>).
    /// </summary>
    public static int[] GetTopHatRadii(int minDim, int userMaxRadius)
    {
        var autoFloor = Math.Max(1, minDim / 16);
        var cap = Math.Min(32, Math.Max(userMaxRadius, Math.Clamp(autoFloor, 1, 16)));

        var rLo = Math.Max(1, minDim / 40);
        var rMid = Math.Clamp(minDim / 24, 1, cap);
        var rFine = Math.Max(1, minDim / 64);

        var set = new HashSet<int>();
        void TryAdd(int r)
        {
            if (r >= 1 && r <= cap)
            {
                set.Add(r);
            }
        }

        TryAdd(Math.Min(rFine, cap));
        TryAdd(Math.Min(rLo, cap));
        TryAdd(Math.Min(rLo + 1, cap));
        TryAdd(Math.Min(rLo + 2, cap));
        TryAdd(Math.Min(rMid, cap));
        TryAdd(Math.Min(3, cap));

        if (set.Count == 0)
        {
            set.Add(1);
        }

        return set.OrderBy(x => x).ToArray();
    }
}
