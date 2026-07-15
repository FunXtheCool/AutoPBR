namespace AutoPBR.App.Rendering.OpenGL;

internal readonly record struct GlGpuDrawReductionSnapshot(
    uint ExaminedCommands,
    uint WrittenCommands,
    uint FrustumCulledCommands,
    uint DistanceCulledCommands,
    uint EmptyCommands,
    uint VisibilityFlagCulledCommands,
    uint OverflowCommands,
    uint MaximumIndexCount)
{
    public const int DwordCount = 8;

    public uint AccountedCommands =>
        WrittenCommands +
        FrustumCulledCommands +
        DistanceCulledCommands +
        EmptyCommands +
        VisibilityFlagCulledCommands +
        OverflowCommands;

    public bool IsConsistent => ExaminedCommands == AccountedCommands;

    public static GlGpuDrawReductionSnapshot FromDwords(ReadOnlySpan<uint> dwords)
    {
        if (dwords.Length < DwordCount)
        {
            throw new ArgumentException("GPU draw reduction snapshot requires eight uints.", nameof(dwords));
        }

        return new GlGpuDrawReductionSnapshot(
            dwords[0],
            dwords[1],
            dwords[2],
            dwords[3],
            dwords[4],
            dwords[5],
            dwords[6],
            dwords[7]);
    }

    public string FormatDiagnostic() =>
        $"examined={ExaminedCommands}, written={WrittenCommands}, " +
        $"frustum={FrustumCulledCommands}, distance={DistanceCulledCommands}, " +
        $"empty={EmptyCommands}, flags={VisibilityFlagCulledCommands}, " +
        $"overflow={OverflowCommands}, maxIndices={MaximumIndexCount}";
}
