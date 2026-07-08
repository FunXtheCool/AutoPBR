using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using DeepBumpInputModeEnum = AutoPBR.Core.Models.DeepBumpInputMode;
using MlSpecularBlendMathEnum = AutoPBR.Core.Models.MlSpecularBlendMath;
using MlSpecularBlendModeEnum = AutoPBR.Core.Models.MlSpecularHeuristicBlendMode;
using NormalDerivativeEnum = AutoPBR.Core.Models.NormalDerivative;
using NormalKernelSizeEnum = AutoPBR.Core.Models.NormalKernelSize;
using NormalOperatorEnum = AutoPBR.Core.Models.NormalOperator;

namespace AutoPBR.App.Models;

/// <summary>
/// Snapshot of conversion-related settings used to build <see cref="AutoPBROptions"/>.
/// Populated from the VM before conversion or preview.
/// </summary>
internal sealed class ConversionSettingsModel
{
    public double NormalIntensity { get; set; } = AutoPBRDefaults.DefaultNormalIntensity;
    public double HeightIntensity { get; set; } = AutoPBRDefaults.DefaultHeightIntensity;
    public bool BrickHeightMapPostProcessEnabled { get; set; } = AutoPBRDefaults.DefaultBrickHeightMapPostProcessEnabled;
    public double BrickHeightMinStructuralConfidence { get; set; } = AutoPBRDefaults.DefaultBrickHeightMinStructuralConfidence;
    public double BrickHeightInvertDeltaThreshold { get; set; } = AutoPBRDefaults.DefaultBrickHeightInvertDeltaThreshold;
    public double BrickLightGroutDiffuseDeltaMin { get; set; } = AutoPBRDefaults.DefaultBrickLightGroutDiffuseDeltaMin;
    /// <summary>Single-texture preview only: populate brick probe diagnostics on the work item.</summary>
    public bool BrickProbePreviewDebug { get; set; }
    public bool FastSpecular { get; set; }
    public bool UseLegacyExtractor { get; set; }
    public double SmoothnessScale { get; set; } = AutoPBRDefaults.DefaultSmoothnessScale;
    public double MetallicBoost { get; set; } = AutoPBRDefaults.DefaultMetallicBoost;
    public double PorosityBias { get; set; } = AutoPBRDefaults.DefaultPorosityBias;

    /// <summary>Extra B offset for plant-tagged textures (added to <see cref="PorosityBias"/>).</summary>
    public double PlantMaterialPorosityExtra { get; set; } = AutoPBRDefaults.DefaultPlantMaterialPorosityExtra;
    public int MaxThreads { get; set; }
    public string? TempDirectory { get; set; }

    /// <summary>Optional local Minecraft install/version folder for block model JSON fallback during 3D preview.</summary>
    public string? MinecraftAssetsDirectory { get; set; }

    public bool ProcessBlocks { get; set; } = true;
    public bool ProcessItems { get; set; } = true;
    public bool ProcessArmor { get; set; } = true;
    public bool ProcessEntity { get; set; } = true;
    public bool ProcessParticles { get; set; } = true;
    public string FoliageMode { get; set; } = "No Height";
    public bool UseDeepBumpNormals { get; set; }
    public string DeepBumpOverlap { get; set; } = "Large";
    public string DeepBumpInputMode { get; set; } = nameof(DeepBumpInputModeEnum.Auto);
    public bool DeepBumpForceBlue255 { get; set; }
    public double DeepBumpNormalIntensity { get; set; } = AutoPBRDefaults.DefaultNormalIntensity;
    public double DeepBumpNormalSoftClamp { get; set; }
    public bool DeepBumpEdgeGuidedEnhance { get; set; }
    public double DeepBumpEdgeGuidedStrength { get; set; } = 1.0;
    public double DeepBumpEdgeGuidedGamma { get; set; } = 1.0;
    public double DeepBumpEdgeGuidedDirectionMix { get; set; } = 0.35;
    /// <summary>0 = only fully transparent pixels (aligned with <see cref="AutoPBROptions.NormalHeightTransparentAlphaClampMax"/> default).</summary>
    public int NormalHeightTransparentAlphaClampMax { get; set; }
    public string NormalOperator { get; set; } = nameof(NormalOperatorEnum.SobelVc);
    public string NormalKernelSize { get; set; } = "3";
    public string NormalDerivative { get; set; } = nameof(NormalDerivativeEnum.Luminance);
    public bool PreprocessLinearize { get; set; }
    public int PreprocessDenoiseRadius { get; set; }
    public double PreprocessDenoiseBlend { get; set; } = 0.5;
    public bool PreprocessFrequencySplit { get; set; }
    public int PreprocessFrequencyRadius { get; set; } = 2;
    public double PreprocessFrequencyDetailStrength { get; set; } = 1.0;

    public bool SpecularUsePercentileRemap { get; set; } = true;
    public double SpecularRemapLowPercentile { get; set; } = 0.02;
    public double SpecularRemapHighPercentile { get; set; } = 0.98;
    /// <summary>When true, specular map alpha is forced to LabPBR &quot;no emission&quot; (255).</summary>
    public bool SpecularForceNoEmissive { get; set; }
    public bool UseMlSpecularPredictor { get; set; }
    public string? MlSpecularModelPath { get; set; }

    /// <summary>
    /// Per-resolution ONNX paths (16, 32, 64, 128, 256). When null, only <see cref="MlSpecularModelPath"/> is used unless
    /// the host merges bundled defaults at conversion time.
    /// </summary>
    public IReadOnlyDictionary<int, string>? MlSpecularModelPathsByResolution { get; set; }

    /// <summary>0 = heuristic specular when ML ran; 1 = full ML weight from the blend toward model output.</summary>
    public double MlSpecularHeuristicBlend { get; set; } = AutoPBRDefaults.DefaultMlSpecularHeuristicBlend;

    /// <summary><see cref="MlSpecularBlendModeEnum"/> as enum name string (e.g. SmoothnessOnly, AiMetalAndEmissive, Full).</summary>
    public string MlSpecularHeuristicBlendMode { get; set; } = nameof(MlSpecularBlendModeEnum.SmoothnessOnly);
    /// <summary><see cref="MlSpecularBlendMathEnum"/> as enum name string (e.g. Linear, Additive, Multiplicative).</summary>
    public string MlSpecularBlendMath { get; set; } = nameof(MlSpecularBlendMathEnum.Linear);

    public bool MlSpecularUseEdgeChannel { get; set; } = true;
    /// <summary>0 = only fully transparent pixels (aligned with <see cref="AutoPBROptions.MlSpecularTransparentAlphaClampMax"/> default).</summary>
    public int MlSpecularTransparentAlphaClampMax { get; set; }
    public bool SpecularDebugDisableHeuristicSpecular { get; set; }
    public bool SpecularDebugSkipSpecularRemap { get; set; }
    public bool SpecularDebugVerboseSpecularMl { get; set; }

    public bool GenerateAo { get; set; }
    public int AoRadius { get; set; } = 4;
    public double AoStrength { get; set; } = 1.0;

    /// <summary>When true, ONNX GPU uses TensorRT EP (CUDA fallback). When false, CUDA only.</summary>
    public bool PreferOnnxTensorRtExecutionProvider { get; set; }

    /// <summary>Builds Core options from this snapshot plus runtime data (specular, ignore set, entries filter, tag overrides, tag rules, semantic options).</summary>
    public AutoPBROptions ToAutoPBROptions(
        SpecularData? specularData,
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualTagOverrides = null,
        IReadOnlyList<TagRule>? tagRules = null,
        MaterialTagSemanticOptions? semanticOptions = null)
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
        var deepBumpInputMode = Enum.TryParse<DeepBumpInputModeEnum>(DeepBumpInputMode, ignoreCase: true,
            out var parsedDeepBumpInputMode)
            ? parsedDeepBumpInputMode
            : DeepBumpInputModeEnum.Auto;
        var mlBlendMode = Enum.TryParse<MlSpecularBlendModeEnum>(MlSpecularHeuristicBlendMode, ignoreCase: true,
            out var parsedMlBlend)
            ? parsedMlBlend
            : MlSpecularBlendModeEnum.SmoothnessOnly;
        var mlBlendMath = Enum.TryParse<MlSpecularBlendMathEnum>(MlSpecularBlendMath, ignoreCase: true,
            out var parsedMlBlendMath)
            ? parsedMlBlendMath
            : MlSpecularBlendMathEnum.Linear;
        return new AutoPBROptions
        {
            NormalIntensity = (float)NormalIntensity,
            HeightIntensity = (float)HeightIntensity,
            BrickHeightMapPostProcessEnabled = BrickHeightMapPostProcessEnabled,
            BrickHeightMinStructuralConfidence = (float)Math.Clamp(BrickHeightMinStructuralConfidence, 0.001, 0.25),
            BrickHeightInvertDeltaThreshold = (float)Math.Clamp(BrickHeightInvertDeltaThreshold, 0.0, 32.0),
            BrickLightGroutDiffuseDeltaMin = (float)Math.Clamp(BrickLightGroutDiffuseDeltaMin, 0.0, 0.25),
            BrickProbePreviewDebug = BrickProbePreviewDebug,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = (float)SmoothnessScale,
            MetallicBoost = (float)MetallicBoost,
            PorosityBias = (int)Math.Round(PorosityBias),
            PlantMaterialPorosityExtra = Math.Clamp((int)Math.Round(PlantMaterialPorosityExtra), -128, 128),
            SpecularUsePercentileRemap = SpecularUsePercentileRemap,
            SpecularRemapLowPercentile = (float)SpecularRemapLowPercentile,
            SpecularRemapHighPercentile = (float)SpecularRemapHighPercentile,
            SpecularForceNoEmissive = SpecularForceNoEmissive,
            UseMlSpecularPredictor = UseMlSpecularPredictor,
            MlSpecularModelPath = string.IsNullOrWhiteSpace(MlSpecularModelPath) ? null : MlSpecularModelPath.Trim(),
            MlSpecularModelPathsByResolution = MlSpecularModelPathsByResolution is { Count: > 0 }
                ? MlSpecularModelPathsByResolution
                : null,
            MlSpecularHeuristicBlend = (float)Math.Clamp(MlSpecularHeuristicBlend, 0.0, 1.0),
            MlSpecularHeuristicBlendMode = mlBlendMode,
            MlSpecularBlendMath = mlBlendMath,
            MlSpecularUseEdgeChannel = MlSpecularUseEdgeChannel,
            MlSpecularTransparentAlphaClampMax = Math.Clamp(MlSpecularTransparentAlphaClampMax, 0, 255),
            SpecularDebugDisableHeuristicSpecular = SpecularDebugDisableHeuristicSpecular,
            SpecularDebugSkipSpecularRemap = SpecularDebugSkipSpecularRemap,
            SpecularDebugVerboseSpecularMl = SpecularDebugVerboseSpecularMl,
            MaxThreads = MaxThreads,
            TempDirectory = string.IsNullOrWhiteSpace(TempDirectory) ? null : TempDirectory,
            MinecraftAssetsDirectory = string.IsNullOrWhiteSpace(MinecraftAssetsDirectory)
                ? null
                : MinecraftAssetsDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessParticles = ProcessParticles,
            GenerateAo = GenerateAo,
            AoRadius = AoRadius,
            AoStrength = (float)AoStrength,
            PreferOnnxTensorRtExecutionProvider = PreferOnnxTensorRtExecutionProvider,
            IgnoreTextureKeys = ignore,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpModelPath = UseDeepBumpNormals
                ? Path.Combine(AppContext.BaseDirectory, "Data", "ONNX-AI", "DeepBump", "deepbump256.onnx")
                : null,
            DeepBumpOverlap = DeepBumpOverlap,
            DeepBumpInputMode = deepBumpInputMode,
            DeepBumpForceBlue255 = DeepBumpForceBlue255,
            DeepBumpNormalIntensity = (float)Math.Clamp(DeepBumpNormalIntensity, 0.05, 8.0),
            DeepBumpNormalSoftClamp = (float)Math.Clamp(DeepBumpNormalSoftClamp, 0.0, 2.0),
            DeepBumpEdgeGuidedEnhance = DeepBumpEdgeGuidedEnhance,
            DeepBumpEdgeGuidedStrength = (float)Math.Clamp(DeepBumpEdgeGuidedStrength, 0.0, 6.0),
            DeepBumpEdgeGuidedGamma = (float)Math.Clamp(DeepBumpEdgeGuidedGamma, 0.1, 8.0),
            DeepBumpEdgeGuidedDirectionMix = (float)Math.Clamp(DeepBumpEdgeGuidedDirectionMix, 0.0, 1.0),
            NormalHeightTransparentAlphaClampMax = Math.Clamp(NormalHeightTransparentAlphaClampMax, 0, 255),
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
            TagRules = tagRules,
            SemanticOptions = semanticOptions
        };
    }
}
