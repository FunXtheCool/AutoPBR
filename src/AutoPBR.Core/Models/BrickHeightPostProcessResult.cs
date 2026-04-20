namespace AutoPBR.Core.Models;

/// <summary>Probe metrics and decisions from brick-tagged height post-processing.</summary>
/// <remarks>
/// <para><see cref="StructuralConfidence"/> — mean normalized mortar response (0–1). <see cref="SkippedLowConfidence"/> is set only when no structural mask could be built (uniform diffuse).</para>
/// <para><see cref="DeltaMortarMinusBrick"/> — mean height on mortar mask minus mean on bulk brick mask (0–255 scale);
/// positive values suggest diffuse height puts joints above faces, so a global invert may help when confidence is high.</para>
/// </remarks>
public readonly record struct BrickHeightPostProcessResult(
    float MeanMortarHeight,
    float MeanBrickHeight,
    float DeltaMortarMinusBrick,
    float StructuralConfidence,
    bool AppliedGlobalInvert,
    bool SkippedLowConfidence,
    string? DebugText = null);
