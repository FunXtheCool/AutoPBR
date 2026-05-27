using CommunityToolkit.Mvvm.Input;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsConverting;

    /// <summary>
    /// Per-resolution paths: explicit text box overrides bundled <c>Data/ONNX-AI/SpecLab</c> models when empty.
    /// </summary>
    private Dictionary<int, string>? BuildMergedSpecularResolutionMap()
    {
        var d = new Dictionary<int, string>();
        var baseDir = AppContext.BaseDirectory;
        void Put(int res, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                d[res] = explicitPath.Trim();
                return;
            }

            var bundled = MlSpecularBundledModelPaths.TryResolveExistingBundledPath(res, baseDir);
            if (bundled is not null)
            {
                d[res] = bundled;
            }
        }

        Put(16, MlSpecularModelPath16);
        Put(32, MlSpecularModelPath32);
        Put(64, MlSpecularModelPath64);
        Put(128, MlSpecularModelPath128);
        Put(256, MlSpecularModelPath256);
        return d.Count > 0 ? d : null;
    }

    /// <summary>Build converter options from current VM state and scan data.</summary>
    private AutoPbrOptions BuildConversionOptions(
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualTagOverrides = null,
        bool brickProbePreviewDebug = false)
    {
        var model = new ConversionSettingsModel
        {
            NormalIntensity = NormalIntensity,
            HeightIntensity = HeightIntensity,
            BrickHeightMapPostProcessEnabled = BrickHeightMapPostProcessEnabled,
            BrickHeightMinStructuralConfidence = BrickHeightMinStructuralConfidence,
            BrickHeightInvertDeltaThreshold = BrickHeightInvertDeltaThreshold,
            BrickLightGroutDiffuseDeltaMin = BrickLightGroutDiffuseDeltaMin,
            BrickProbePreviewDebug = brickProbePreviewDebug,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = SmoothnessScale,
            MetallicBoost = MetallicBoost,
            PorosityBias = PorosityBias,
            PlantMaterialPorosityExtra = PlantMaterialPorosityExtra,
            SpecularUsePercentileRemap = SpecularUsePercentileRemap,
            SpecularRemapLowPercentile = SpecularRemapLowPercentile,
            SpecularRemapHighPercentile = SpecularRemapHighPercentile,
            SpecularForceNoEmissive = SpecularForceNoEmissive,
            UseMlSpecularPredictor = UseMlSpecularPredictor,
            MlSpecularModelPath = MlSpecularModelPath,
            MlSpecularModelPathsByResolution = BuildMergedSpecularResolutionMap(),
            MlSpecularHeuristicBlend = MlSpecularHeuristicBlend,
            MlSpecularHeuristicBlendMode = MlSpecularHeuristicBlendMode,
            MlSpecularBlendMath = MlSpecularBlendMath,
            MlSpecularUseEdgeChannel = MlSpecularUseEdgeChannel,
            MlSpecularTransparentAlphaClampMax = MlSpecularTransparentAlphaClampMax,
            SpecularDebugDisableHeuristicSpecular = SpecularDebugDisableHeuristicSpecular,
            SpecularDebugSkipSpecularRemap = SpecularDebugSkipSpecularRemap,
            SpecularDebugVerboseSpecularMl = SpecularDebugVerboseSpecularMl,
            MaxThreads = MaxThreads,
            TempDirectory = TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessEntity = ProcessEntity,
            ProcessParticles = ProcessParticles,
            GenerateAo = GenerateAo,
            AoRadius = AoRadius,
            AoStrength = AoStrength,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpOverlap = DeepBumpOverlap,
            DeepBumpInputMode = DeepBumpInputMode,
            DeepBumpForceBlue255 = DeepBumpForceBlue255,
            DeepBumpNormalIntensity = DeepBumpNormalIntensity,
            DeepBumpNormalSoftClamp = DeepBumpNormalSoftClamp,
            DeepBumpEdgeGuidedEnhance = DeepBumpEdgeGuidedEnhance,
            DeepBumpEdgeGuidedStrength = DeepBumpEdgeGuidedStrength,
            DeepBumpEdgeGuidedGamma = DeepBumpEdgeGuidedGamma,
            DeepBumpEdgeGuidedDirectionMix = DeepBumpEdgeGuidedDirectionMix,
            NormalHeightTransparentAlphaClampMax = NormalHeightTransparentAlphaClampMax,
            NormalOperator = NormalOperator,
            NormalKernelSize = NormalKernelSize,
            NormalDerivative = NormalDerivative,
            PreprocessLinearize = PreprocessLinearize,
            PreprocessDenoiseRadius = PreprocessDenoiseRadius,
            PreprocessDenoiseBlend = PreprocessDenoiseBlend,
            PreprocessFrequencySplit = PreprocessFrequencySplit,
            PreprocessFrequencyRadius = PreprocessFrequencyRadius,
            PreprocessFrequencyDetailStrength = PreprocessFrequencyDetailStrength,
            PreferOnnxTensorRtExecutionProvider = PreferOnnxTensorRtExecutionProvider
        };
        return model.ToAutoPbrOptions(_specularData, ignore, entriesToExtractOnly,
            manualTagOverrides ?? _exploreController.GetManualTagOverrides(), GetEffectiveTagRules(),
            BuildMaterialTagSemanticOptions());
    }


}
