using System.Text.Json;
using System.Text.Json.Serialization;

using AutoPBR.Core;
using AutoPBR.Core.Models;

using DeepBumpInputModeEnum = AutoPBR.Core.Models.DeepBumpInputMode;
using NormalDerivativeEnum = AutoPBR.Core.Models.NormalDerivative;
using NormalOperatorEnum = AutoPBR.Core.Models.NormalOperator;

namespace AutoPBR.App.Models;

public sealed class UserSettings
{
    public string? OutputDirectory { get; set; }

    /// <summary>Folder scanned for batch .zip / .jar resource packs (Scan tab).</summary>
    public string? BatchFolderPath { get; set; }

    /// <summary>When true, input UI targets batch folder; main Convert runs batch for that folder.</summary>
    public bool UseBatchFolderInput { get; set; }
    public double NormalIntensity { get; set; } = AutoPbrDefaults.DefaultNormalIntensity;
    public double HeightIntensity { get; set; } = AutoPbrDefaults.DefaultHeightIntensity;
    public bool BrickHeightMapPostProcessEnabled { get; set; } = AutoPbrDefaults.DefaultBrickHeightMapPostProcessEnabled;
    public double BrickHeightMinStructuralConfidence { get; set; } = AutoPbrDefaults.DefaultBrickHeightMinStructuralConfidence;
    public double BrickHeightInvertDeltaThreshold { get; set; } = AutoPbrDefaults.DefaultBrickHeightInvertDeltaThreshold;
    public double BrickLightGroutDiffuseDeltaMin { get; set; } = AutoPbrDefaults.DefaultBrickLightGroutDiffuseDeltaMin;
    public bool PreviewBrickProbeDebug { get; set; } = AutoPbrDefaults.DefaultBrickProbePreviewDebug;
    public bool FastSpecular { get; set; }
    public string FoliageMode { get; set; } = "No Height";

    /// <summary>Legacy: when loading old settings with "IgnorePlants": false, migrate to FoliageMode "Convert All".</summary>
    [JsonPropertyName("IgnorePlants")]
    public bool? IgnorePlants
    {
        set => FoliageMode = value == false ? "Convert All" : "Ignore All";
    }

    public bool UseLegacyExtractor { get; set; }

    /// <summary>Backward compat: old settings used "ExperimentalExtractor" (true = parallel). Map to UseLegacyExtractor = !value.</summary>
    [JsonPropertyName("ExperimentalExtractor")]
    public bool ExperimentalExtractor
    {
        get => !UseLegacyExtractor;
        set => UseLegacyExtractor = !value;
    }

    public double SmoothnessScale { get; set; } = AutoPbrDefaults.DefaultSmoothnessScale;
    public double MetallicBoost { get; set; } = AutoPbrDefaults.DefaultMetallicBoost;
    public double PorosityBias { get; set; } = AutoPbrDefaults.DefaultPorosityBias;

    /// <summary>Extra porosity B for plant-tagged textures (added to <see cref="PorosityBias"/>). Null = use default on load.</summary>
    public double? PlantMaterialPorosityExtra { get; set; }
    public int MaxThreads { get; set; } // 0 = auto
    public string? TempDirectory { get; set; }
    public bool DebugMode { get; set; }
    public string ColorScheme { get; set; } = "Dark";

    /// <summary>Interface scale (typically 0.75–1.75). 1.0 = 100%.</summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>UI language culture code (e.g. "en", "de").</summary>
    public string Language { get; set; } = "en";

    public bool ProcessBlocks { get; set; } = true;
    public bool ProcessItems { get; set; } = true;
    public bool ProcessArmor { get; set; } = true;
    public bool ProcessEntity { get; set; } = true;
    public bool ProcessParticles { get; set; } = true;
    public bool UseDeepBumpNormals { get; set; }

    /// <summary>DeepBump tile overlap: "Small", "Medium", or "Large" (default Large = best quality).</summary>
    public string DeepBumpOverlap { get; set; } = "Large";

    public string DeepBumpInputMode { get; set; } = nameof(DeepBumpInputModeEnum.Auto);
    public bool DeepBumpForceBlue255 { get; set; }
    public double DeepBumpNormalIntensity { get; set; } = AutoPbrDefaults.DefaultNormalIntensity;
    public double DeepBumpNormalSoftClamp { get; set; }
    public bool DeepBumpEdgeGuidedEnhance { get; set; }
    public double DeepBumpEdgeGuidedStrength { get; set; } = 1.0;
    public double DeepBumpEdgeGuidedGamma { get; set; } = 1.0;
    public double DeepBumpEdgeGuidedDirectionMix { get; set; } = 0.35;
    public int NormalHeightTransparentAlphaClampMax { get; set; }

    /// <summary>Normal operator when not using DeepBump. \"SobelVc\" or \"ScharrVc\".</summary>
    public string NormalOperator { get; set; } = nameof(NormalOperatorEnum.SobelVc);

    /// <summary>Normal kernel size for Sobel/Scharr when not using DeepBump. \"3\", \"5\", or \"7\" (7 only for Sobel).</summary>
    public string NormalKernelSize { get; set; } = "3";

    /// <summary>What to derive normals from: Luminance, Color, ColorLuminanceBlend, or ColorLuminanceMax.</summary>
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
    public bool SpecularForceNoEmissive { get; set; }
    public bool UseMlSpecularPredictor { get; set; }
    public string? MlSpecularModelPath { get; set; }

    /// <summary>Optional override paths for bundled per-resolution specular models (edge length in pixels).</summary>
    public string? MlSpecularModelPath16 { get; set; }
    public string? MlSpecularModelPath32 { get; set; }
    public string? MlSpecularModelPath64 { get; set; }
    public string? MlSpecularModelPath128 { get; set; }
    public string? MlSpecularModelPath256 { get; set; }

    /// <summary>Blend toward ML specular (0 = heuristic, 1 = full ML). Null = use default on load.</summary>
    public double? MlSpecularHeuristicBlend { get; set; }

    /// <summary>Specular ML blend mode enum name (SmoothnessOnly, AiMetalAndEmissive, or Full); null defaults to SmoothnessOnly.</summary>
    public string? MlSpecularHeuristicBlendMode { get; set; }
    /// <summary>Specular ML blend math enum name (Linear, Additive, Multiplicative); null defaults to Linear.</summary>
    public string? MlSpecularBlendMath { get; set; }

    public bool MlSpecularUseEdgeChannel { get; set; } = true;
    public int MlSpecularTransparentAlphaClampMax { get; set; }
    public bool SpecularDebugDisableHeuristicSpecular { get; set; }
    public bool SpecularDebugSkipSpecularRemap { get; set; }
    public bool SpecularDebugVerboseSpecularMl { get; set; }

    public bool GenerateAo { get; set; }
    public int AoRadius { get; set; } = 4;
    public double AoStrength { get; set; } = 1.0;

    /// <summary>When true, ONNX GPU sessions prefer TensorRT (CUDA fallback). Default false = CUDA only.</summary>
    public bool PreferOnnxTensorRtExecutionProvider { get; set; }

    /// <summary>User-defined material tag rules (keywords + optional semantic hints).</summary>
    public List<CustomTagRuleEntry> CustomTagRules { get; set; } = [];

    /// <summary>When true, Explore suggests extra tags using MiniLM embeddings (requires ONNX model under Data).</summary>
    public bool UseSemanticMaterialTags { get; set; } = true;

    /// <summary>Minimum cosine similarity (0–1) for a semantic tag suggestion.</summary>
    public double MaterialTagMinSimilarity { get; set; } = 0.25;

    /// <summary>When the best ML score is below this (0–1), only the Unknown material tag is suggested.</summary>
    public double MaterialTagCertaintyThreshold { get; set; } = 0.35;

    /// <summary>Maximum number of ML-suggested tags per texture.</summary>
    public int MaterialTagMaxCount { get; set; } = 3;

    /// <summary>When true, semantic matching augments MiniLM using dictionary definition evidence.</summary>
    public bool DictionaryEvidenceEnabled { get; set; }

    /// <summary>Dictionary evidence blend weight (0..1) for fused semantic score.</summary>
    public double DictionaryEvidenceWeight { get; set; } = 0.35;

    /// <summary>Minimum dictionary evidence cosine score used for fusion.</summary>
    public double DictionaryMinEvidenceScore { get; set; } = 0.18;

    /// <summary>Dictionary request timeout in milliseconds.</summary>
    public int DictionaryRequestTimeoutMs { get; set; } = 900;

    /// <summary>0 = 2D preview, 1 = 3D preview.</summary>
    public int PreviewDisplayMode { get; set; }

    /// <summary>When in 3D preview mode, continuously rotate the block preview.</summary>
    public bool Preview3DAutoRotate { get; set; } = true;

    /// <summary>3D preview: speed multiplier for emulated entity idle animation.</summary>
    public double Preview3DEntityAnimationSpeed { get; set; } = 1.0;

    /// <summary>3D preview: amplitude multiplier for emulated entity idle animation.</summary>
    public double Preview3DEntityAnimationAmplitude { get; set; } = 1.0;

    /// <summary>3D preview: enable/disable emulated entity idle animation.</summary>
    public bool Preview3DEnableEntityAnimation { get; set; } = true;

    /// <summary>Legacy preview bob/yaw on emulated entities (independent of setupAnim IR motion).</summary>
    public bool Preview3DEnableLegacyEntityWobble { get; set; }

    /// <summary>3D preview: freeze vertex-baked emulated entity idle motion at the current clock.</summary>
    public bool Preview3DPauseEntityIdleAnimation { get; set; }

    /// <summary>When previewing sprite-style foliage in 3D, blend translucent pixels instead of alpha cutout.</summary>
    public bool Preview3DItemUseAlphaBlend { get; set; }

    /// <summary>Emulated entity 3D preview diffuse alpha handling: 0 opaque, 1 cutout (default), 2 blend.</summary>
    public int Preview3DEntityAlphaMode { get; set; } = 1;

    /// <summary>3D preview: use LabPBR normal and specular maps on runtime entity rigs.</summary>
    public bool Preview3DEnableEntityLabPbrShading { get; set; } = true;

    /// <summary>3D preview: enable POM / parallax AO / parallax self-shadow on runtime entity rigs.</summary>
    public bool Preview3DEnableEntityParallax { get; set; }

    /// <summary>Draw a ground grid in 3D preview.</summary>
    public bool Preview3DShowGrid { get; set; } = true;

    /// <summary>Draw X/Y/Z axis reference in the corner of the 3D preview.</summary>
    public bool Preview3DShowAxes { get; set; } = true;

    /// <summary>When true, 3D preview uses clamped parallax (POM) from the height map.</summary>
    public bool Preview3DEnableParallax { get; set; } = true;

    /// <summary>When true, 3D preview applies LabPBR-style normal mapping (_n).</summary>
    public bool Preview3DEnableNormalMap { get; set; } = true;

    /// <summary>When true, 3D preview applies LabPBR-style specular / metal interpretation (_s).</summary>
    public bool Preview3DEnableSpecularMap { get; set; } = true;

    /// <summary>Genesis: shader-side parallax displacement scalar (height strength, 0..0.35).</summary>
    public double Preview3DParallaxHeightStrength { get; set; } = 0.05;

    /// <summary>Genesis: enable cheap subsurface scattering approximation (LabPBR _s.b >= 65).</summary>
    public bool Preview3DEnableSss { get; set; } = true;

    /// <summary>Genesis: enable parallax self-shadow trace toward the light.</summary>
    public bool Preview3DEnableParallaxShadow { get; set; } = true;

    /// <summary>Genesis: toggle POM-derived contact AO in the 3D preview shader.</summary>
    public bool Preview3DEnableParallaxAo { get; set; } = true;

    /// <summary>Genesis: strength multiplier for POM-derived contact AO (0..2).</summary>
    public double Preview3DParallaxAoStrength { get; set; } = 1.0;

    /// <summary>Genesis: enable environment IBL (LUT-based when atmospheric sky is on; procedural hemisphere otherwise).</summary>
    public bool Preview3DEnableIbl { get; set; } = true;

    /// <summary>Atmospheric sky: render LUT-driven sky background and ambient probes.</summary>
    public bool Preview3DEnableAtmosphericSky { get; set; } = true;

    /// <summary>Atmospheric sky turbidity (haze amount).</summary>
    public double Preview3DAtmosphereTurbidity { get; set; } = 2.6;

    /// <summary>Atmospheric sky sun intensity multiplier.</summary>
    public double Preview3DAtmosphereSunIntensity { get; set; } = 16.0;

    /// <summary>Atmospheric sky horizon falloff scalar.</summary>
    public double Preview3DAtmosphereHorizonFalloff { get; set; } = 1.35;

    /// <summary>Genesis Shadows Phase 2: master toggle for the directional shadow map pass.</summary>
    public bool Preview3DEnableShadows { get; set; } = true;

    /// <summary>Genesis Shadows Phase 2: light yaw in degrees (-180..180); drives the shadow ortho frustum.</summary>
    public double Preview3DLightYawDegrees { get; set; } = -35;

    /// <summary>Genesis Shadows Phase 2: light pitch in degrees (-89..89, negative = sun above horizon).</summary>
    public double Preview3DLightPitchDegrees { get; set; } = -55;

    /// <summary>PHASE3-CSM stub: persisted toggle for cascaded shadow maps; no UI in Phase 2.</summary>
    public bool Preview3DEnableShadowCascades { get; set; }

    /// <summary>Number of crossed sprite planes to build for 2D Sprite flagged textures in 3D preview.</summary>
    public int Preview3DSpritePlaneCount { get; set; } = 1;

    /// <summary>3D preview: orbit sensitivity in radians per pixel (Alt + middle mouse).</summary>
    public double Preview3DCameraOrbitSensitivity { get; set; } = 0.006;

    /// <summary>3D preview: pan sensitivity (middle mouse without Alt); scaled by camera distance.</summary>
    public double Preview3DCameraPanSensitivity { get; set; } = 0.0022;

    /// <summary>3D preview: mouse wheel zoom step strength.</summary>
    public double Preview3DCameraZoomSensitivity { get; set; } = 0.12;

    /// <summary>3D preview: orbit boom arm length (world units), pivot-to-eye distance for default framing.</summary>
    public double Preview3DCameraOrbitBoomDistance { get; set; } =
        Math.Sqrt(3.6 * 3.6 + 2.6 * 2.6 + 3.6 * 3.6);

    /// <summary>3D preview: keyboard key name to reset camera (Avalonia Key enum name, e.g. R, Home, Escape).</summary>
    public string Preview3DCameraResetKey { get; set; } = "R";

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
    private static readonly JsonSerializerOptions SaveJsonSerializerOptions = new() { WriteIndented = true };

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {

                return new UserSettings();
            }


            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return settings ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, SaveJsonSerializerOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort; ignore persistence errors.
        }
    }
}

