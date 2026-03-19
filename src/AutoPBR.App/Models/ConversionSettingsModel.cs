using AutoPBR.Core;
using AutoPBR.Core.Models;
using NormalOperatorEnum = AutoPBR.Core.Models.NormalOperator;
using NormalKernelSizeEnum = AutoPBR.Core.Models.NormalKernelSize;
using NormalDerivativeEnum = AutoPBR.Core.Models.NormalDerivative;
using QualityProfileEnum = AutoPBR.Core.Models.QualityProfile;
using DeepBumpInputModeEnum = AutoPBR.Core.Models.DeepBumpInputMode;

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
    public string DeepBumpInputMode { get; set; } = nameof(DeepBumpInputModeEnum.Auto);
    public bool DeepBumpForceBlue255 { get; set; }
    public string NormalOperator { get; set; } = nameof(NormalOperatorEnum.SobelVc);
    public string NormalKernelSize { get; set; } = "3";
    public string NormalDerivative { get; set; } = nameof(NormalDerivativeEnum.Luminance);
    public string QualityProfile { get; set; } = nameof(QualityProfileEnum.Balanced);

    public bool PreprocessLinearize { get; set; }
    public int PreprocessDenoiseRadius { get; set; }
    public double PreprocessDenoiseBlend { get; set; } = 0.5;
    public bool PreprocessFrequencySplit { get; set; }
    public int PreprocessFrequencyRadius { get; set; } = 2;
    public double PreprocessFrequencyDetailStrength { get; set; } = 1.0;

    public bool SpecularUsePercentileRemap { get; set; } = true;
    public double SpecularRemapLowPercentile { get; set; } = 0.02;
    public double SpecularRemapHighPercentile { get; set; } = 0.98;
    public string? MetalHeuristicSubstrings { get; set; }

    public bool GenerateAo { get; set; }
    public int AoRadius { get; set; } = 4;
    public double AoStrength { get; set; } = 1.0;

    /// <summary>Builds Core options from this snapshot plus runtime data (specular, ignore set, entries filter, tag overrides, tag rules).</summary>
    public AutoPbrOptions ToAutoPbrOptions(
        SpecularData? specularData,
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualTagOverrides = null,
        IReadOnlyList<TagRule>? tagRules = null)
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
        var profile = Enum.TryParse<QualityProfileEnum>(QualityProfile, ignoreCase: true, out var parsedProfile)
            ? parsedProfile
            : QualityProfileEnum.Balanced;
        var deepBumpInputMode = Enum.TryParse<DeepBumpInputModeEnum>(DeepBumpInputMode, ignoreCase: true,
            out var parsedDeepBumpInputMode)
            ? parsedDeepBumpInputMode
            : DeepBumpInputModeEnum.Auto;

        IReadOnlyList<string>? metalSubs = null;
        if (!string.IsNullOrWhiteSpace(MetalHeuristicSubstrings))
        {
            metalSubs = MetalHeuristicSubstrings
                .Split([',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return new AutoPbrOptions
        {
            QualityProfile = profile,
            NormalIntensity = (float)NormalIntensity,
            HeightIntensity = (float)HeightIntensity,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = (float)SmoothnessScale,
            MetallicBoost = (float)MetallicBoost,
            PorosityBias = (int)Math.Round(PorosityBias),
            SpecularUsePercentileRemap = SpecularUsePercentileRemap,
            SpecularRemapLowPercentile = (float)SpecularRemapLowPercentile,
            SpecularRemapHighPercentile = (float)SpecularRemapHighPercentile,
            MetalHeuristicSubstrings = metalSubs,
            MaxThreads = MaxThreads,
            TempDirectory = string.IsNullOrWhiteSpace(TempDirectory) ? null : TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessParticles = ProcessParticles,
            GenerateAo = GenerateAo,
            AoRadius = AoRadius,
            AoStrength = (float)AoStrength,
            IgnoreTextureKeys = ignore,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpModelPath = UseDeepBumpNormals
                ? Path.Combine(AppContext.BaseDirectory, "Data", "deepbump256.onnx")
                : null,
            DeepBumpOverlap = DeepBumpOverlap,
            DeepBumpInputMode = deepBumpInputMode,
            DeepBumpForceBlue255 = DeepBumpForceBlue255,
            NormalOperator = op,
            NormalKernelSize = ks,
            NormalDerivative = deriv,
            PreprocessLinearize = PreprocessLinearize,
            PreprocessDenoiseRadius = PreprocessDenoiseRadius,
            PreprocessDenoiseBlend = (float)PreprocessDenoiseBlend,
            PreprocessFrequencySplit = PreprocessFrequencySplit,
            PreprocessFrequencyRadius = PreprocessFrequencyRadius,
            PreprocessFrequencyDetailStrength = (float)PreprocessFrequencyDetailStrength,
            SpecularData = specularData,
            EntriesToExtractOnly = entriesToExtractOnly,
            ManualTagOverrides = manualTagOverrides,
            TagRules = tagRules
        };
    }
}
