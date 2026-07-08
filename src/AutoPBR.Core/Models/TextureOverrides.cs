namespace AutoPBR.Core.Models;

public sealed class TextureOverrides
{
    public float? NormalIntensity { get; set; }
    public bool InvertNormalRed { get; set; }
    public bool InvertNormalGreen { get; set; }

    public float? HeightIntensity { get; set; }
    public float? HeightBrightness { get; set; }
    /// <summary>Invert heightmap values after generation (e.g. automatic coal ore tuning).</summary>
    public bool InvertHeight { get; set; }

    public bool? FastSpecular { get; set; }
    public IReadOnlyList<SpecularRule>? CustomSpecularRules { get; set; }

    /// <summary>
    /// When true, invert the specular smoothness (R) channel after heuristic/ML composition (LabPBR R) so dark↔light swap;
    /// set automatically for the <c>brick</c> material rule when <see cref="BrickProbeAppliedGlobalInvert"/> is not set (legacy fallback), or manually for grout-style fixes.
    /// </summary>
    public bool InvertSpecular { get; set; }

    /// <summary>
    /// When normals/height run first (conversion order), <c>brick</c> + brick height post-process stores the same global invert decision as height here so specular R can match.
    /// Null when brick height rules did not run or did not apply (use <see cref="InvertSpecular"/>).
    /// </summary>
    public bool? BrickProbeAppliedGlobalInvert { get; set; }
}
