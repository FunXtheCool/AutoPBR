namespace AutoPBR.Core.Preview;

/// <summary>
/// How parity-catalog previews combine bytecode-lifted geometry IR with preview motion
/// (see <c>minecraft_26.1.2_geometry_ir_parity_policy.json</c>).
/// </summary>
public enum GeometryIrParityTier
{
    /// <summary>Lifted geometry IR bind mesh only (no mandatory preview animation pass).</summary>
    PreferIr,

    /// <summary>Lifted geometry IR bind mesh plus catalog preview animation (e.g. idle head/wing/leg drivers).</summary>
    IrGeometryPreviewAnim,
}
