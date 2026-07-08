namespace AutoPBR.Contracts.GeometryIr;

/// <summary>
/// Controls preview mesh emission fidelity for geometry IR (parity vs viewport visibility).
/// </summary>
public enum GeometryIrEmitFidelity
{
    /// <summary>No degenerate-axis thickening; matches vanilla cuboid corners for parity tests.</summary>
    Parity,

    /// <summary>Thickens zero-extent axes for GL visibility in the interactive preview.</summary>
    Viewport
}
