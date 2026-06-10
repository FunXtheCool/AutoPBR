namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>Low / medium / high volumetric effect cost profiles for froxel god rays and clouds.</summary>
public static class PreviewVolumetricQuality
{
    public readonly record struct Profile(
        int FroxelDivisor,
        int FroxelMinSize,
        int FroxelSlices,
        float FroxelDepthExp,
        float FroxelTemporal3DWeight,
        float CloudTemporalWeight,
        int CloudQuality,
        float VolumeIntegrateTemporalWeight,
        float UpsampleTemporalWeight)
    {
        public int ResolveFroxelWidth(int viewportWidth) =>
            Math.Max(FroxelMinSize, viewportWidth / FroxelDivisor);

        public int ResolveFroxelHeight(int viewportHeight) =>
            Math.Max(FroxelMinSize, viewportHeight / FroxelDivisor);
    }

    public static Profile Resolve(int quality) =>
        Math.Clamp(quality, 0, 2) switch
        {
            0 => new Profile(FroxelDivisor: 8, FroxelMinSize: 24, FroxelSlices: 12, FroxelDepthExp: 0f,
                FroxelTemporal3DWeight: 0f, CloudTemporalWeight: 0f, CloudQuality: 0,
                VolumeIntegrateTemporalWeight: 0f, UpsampleTemporalWeight: 0f),
            2 => new Profile(FroxelDivisor: 3, FroxelMinSize: 48, FroxelSlices: 24, FroxelDepthExp: 4.2f,
                FroxelTemporal3DWeight: 0.38f, CloudTemporalWeight: 0.55f, CloudQuality: 2,
                VolumeIntegrateTemporalWeight: 0.42f, UpsampleTemporalWeight: 0.55f),
            _ => new Profile(FroxelDivisor: 4, FroxelMinSize: 32, FroxelSlices: 20, FroxelDepthExp: 2.8f,
                FroxelTemporal3DWeight: 0.28f, CloudTemporalWeight: 0.42f, CloudQuality: 1,
                VolumeIntegrateTemporalWeight: 0.35f, UpsampleTemporalWeight: 0.45f),
        };
}
