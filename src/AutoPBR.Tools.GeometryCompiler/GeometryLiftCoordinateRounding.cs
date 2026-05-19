namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Canonical coordinate rounding for lifted geometry IR JSON (cuboid bounds, pose, inflate).
/// </summary>
internal static class GeometryLiftCoordinateRounding
{
    /// <summary>
    /// Rounds a lifted scalar to ten decimal places so JSON fingerprints and comparisons stay stable.
    /// </summary>
    public static double Round(double d) => Math.Round(d, 10);
}
