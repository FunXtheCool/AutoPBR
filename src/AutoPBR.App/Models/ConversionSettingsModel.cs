using AutoPBR.Core;
using AutoPBR.Core.Models;
using NormalOperatorEnum = AutoPBR.Core.Models.NormalOperator;
using NormalKernelSizeEnum = AutoPBR.Core.Models.NormalKernelSize;
using NormalDerivativeEnum = AutoPBR.Core.Models.NormalDerivative;

namespace AutoPBR.App.Models;

/// <summary>
/// Snapshot of conversion-related settings used to build <see cref="AutoPbrOptions"/>.
/// Populated from the VM before conversion or preview.
/// </summary>
internal sealed class ConversionSettingsModel
{
    public double NormalIntensity { get; set; } = AutoPbrDefaults.DefaultNormalIntensity;
    public double HeightIntensity { get; set; } = AutoPbrDefaults.DefaultHeightIntensity;
    public bool FastSpecular { get; set; }
    public bool UseLegacyExtractor { get; set; }
    public double SmoothnessScale { get; set; } = AutoPbrDefaults.DefaultSmoothnessScale;
    public double MetallicBoost { get; set; } = AutoPbrDefaults.DefaultMetallicBoost;
    public double PorosityBias { get; set; } = AutoPbrDefaults.DefaultPorosityBias;
    public int MaxThreads { get; set; }
    public string? TempDirectory { get; set; }
    public bool ProcessBlocks { get; set; } = true;
    public bool ProcessItems { get; set; } = true;
    public bool ProcessArmor { get; set; } = true;
    public bool ProcessEntity { get; set; } = true;
    public bool ProcessParticles { get; set; } = true;
    public string FoliageMode { get; set; } = "Ignore All";
    public bool UseDeepBumpNormals { get; set; }
    public string DeepBumpOverlap { get; set; } = "Large";
    public string NormalOperator { get; set; } = nameof(NormalOperatorEnum.SobelVc);
    public string NormalKernelSize { get; set; } = "3";
    public string NormalDerivative { get; set; } = nameof(NormalDerivativeEnum.Luminance);

    /// <summary>Builds Core options from this snapshot plus runtime data (specular, ignore set, entries filter).</summary>
    public AutoPbrOptions ToAutoPbrOptions(
        SpecularData? specularData,
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly)
    {
        var op = Enum.TryParse<NormalOperatorEnum>(NormalOperator, ignoreCase: true, out var parsedOp)
            ? parsedOp
            : NormalOperatorEnum.SobelVc;
        var ks = NormalKernelSize switch
        {
            "5" => NormalKernelSizeEnum.K5,
            "7" => NormalKernelSizeEnum.K7,
            _ => NormalKernelSizeEnum.K3
        };
        var deriv = Enum.TryParse<NormalDerivativeEnum>(NormalDerivative, ignoreCase: true, out var parsedDeriv)
            ? parsedDeriv
            : NormalDerivativeEnum.Luminance;

        return new AutoPbrOptions
        {
            NormalIntensity = (float)NormalIntensity,
            HeightIntensity = (float)HeightIntensity,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = (float)SmoothnessScale,
            MetallicBoost = (float)MetallicBoost,
            PorosityBias = (int)Math.Round(PorosityBias),
            MaxThreads = MaxThreads,
            TempDirectory = string.IsNullOrWhiteSpace(TempDirectory) ? null : TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessParticles = ProcessParticles,
            IgnoreTextureKeys = ignore,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpModelPath = UseDeepBumpNormals
                ? Path.Combine(AppContext.BaseDirectory, "Data", "deepbump256.onnx")
                : null,
            DeepBumpOverlap = DeepBumpOverlap,
            NormalOperator = op,
            NormalKernelSize = ks,
            NormalDerivative = deriv,
            SpecularData = specularData,
            EntriesToExtractOnly = entriesToExtractOnly
        };
    }
}
