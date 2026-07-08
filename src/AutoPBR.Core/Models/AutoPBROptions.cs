using AutoPBR.Core.Embeddings;

namespace AutoPBR.Core.Models;

public sealed class AutoPBROptions
{
    // Members that match CLR defaults (false / 0) omit redundant initializers; behavior is unchanged.

    public float NormalIntensity { get; init; } = AutoPBRDefaults.DefaultNormalIntensity;
    public float HeightIntensity { get; init; } = AutoPBRDefaults.DefaultHeightIntensity;
    public bool FastSpecular { get; init; }

    /// <summary>
    /// Normal generation operator when not using DeepBump. "SobelVc" = current default
    /// Sobel + VC filter. "ScharrVc" = Scharr gradients + VC filter for stronger, more
    /// isotropic edge response.
    /// </summary>
    public NormalOperator NormalOperator { get; init; } = NormalOperator.SobelVc;

    /// <summary>
    /// Kernel size for Sobel/Scharr normals when not using DeepBump. For Sobel, supports 3x3, 5x5, 7x7.
    /// For Scharr, supports 3x3 and 5x5 (7x7 will be clamped to 5x5).
    /// </summary>
    public NormalKernelSize NormalKernelSize { get; init; } = NormalKernelSize.K3;

    /// <summary>
    /// What to derive normals from when not using DeepBump: Luminance, Color, Color+Luminance blend, or max.
    /// </summary>
    public NormalDerivative NormalDerivative { get; init; } = NormalDerivative.Luminance;

    /// <summary>
    /// When true, convert sRGB bytes to linear-light before deriving luminance/gradients.
    /// Improves physical plausibility but can reduce perceived contrast on very low-res pixel art.
    /// </summary>
    public bool PreprocessLinearize { get; init; }

    /// <summary>
    /// Optional denoise radius applied to derived luminance before gradients (0 = off). Low-res friendly.
    /// </summary>
    public int PreprocessDenoiseRadius { get; init; }

    /// <summary>
    /// Blend factor for denoise (0 = keep original, 1 = fully denoised). Only used when radius &gt; 0.
    /// </summary>
    public float PreprocessDenoiseBlend { get; init; } = 0.5f;

    /// <summary>
    /// When true, apply a simple frequency-split to luminance before gradients: low-pass + (high-pass * strength).
    /// </summary>
    public bool PreprocessFrequencySplit { get; init; }

    /// <summary>Radius for the low-pass used by frequency split (pixels).</summary>
    public int PreprocessFrequencyRadius { get; init; } = 2;

    /// <summary>High-frequency contribution multiplier for frequency split.</summary>
    public float PreprocessFrequencyDetailStrength { get; init; } = 1f;

    /// <summary>
    /// When true, use the legacy ZipFile-based extractor instead of the default parallel extractor.
    /// Use only if you hit issues with the default (e.g. exotic zip format).
    /// </summary>
    public bool UseLegacyExtractor { get; init; }

    /// <summary>
    /// When non-null and non-empty, only these zip entry paths are extracted (e.g. from a prior scan).
    /// Reduces extraction time and disk use when only .png textures (and pack.mcmeta) are needed.
    /// </summary>
    public IReadOnlyList<string>? EntriesToExtractOnly { get; init; }

    /// <summary>
    /// Maximum worker threads to use for conversion (specular/normal/height). 0 or less = auto (CPU-2, minimum 1).
    /// </summary>
    public int MaxThreads { get; init; }

    /// <summary>
    /// Optional base directory for temporary working files. When null or empty, the system temp directory is used.
    /// </summary>
    public string? TempDirectory { get; init; }

    /// <summary>
    /// Optional local Minecraft install or extracted version folder used to resolve missing
    /// <c>assets/minecraft/models/block/*.json</c> during 3D preview (pack zip takes priority).
    /// </summary>
    public string? MinecraftAssetsDirectory { get; init; }

    /// <summary>Scale for dielectric smoothness (R channel). 1 = unchanged; 0.5–1.5 typical.</summary>
    public float SmoothnessScale { get; init; } = AutoPBRDefaults.DefaultSmoothnessScale;

    /// <summary>Boost for metal smoothness (R channel). 1 = unchanged.</summary>
    public float MetallicBoost { get; init; } = AutoPBRDefaults.DefaultMetallicBoost;

    /// <summary>Offset added to porosity/subsurface (B channel). Can be negative.</summary>
    public int PorosityBias { get; init; } = AutoPBRDefaults.DefaultPorosityBias;

    /// <summary>
    /// Extra porosity B offset for textures with the organic material tag (or OptiFine plant/plants paths).
    /// Added to <see cref="PorosityBias"/> for heuristic and ML specular B.
    /// </summary>
    public int PlantMaterialPorosityExtra { get; init; } = AutoPBRDefaults.DefaultPlantMaterialPorosityExtra;

    /// <summary>
    /// When true, normalize per-texture smoothness (R) using percentile remap (more robust than min/max on noisy low-res inputs).
    /// </summary>
    public bool SpecularUsePercentileRemap { get; init; } = true;

    /// <summary>Low percentile (0..1) for smoothness remap window.</summary>
    public float SpecularRemapLowPercentile { get; init; } = 0.02f;

    /// <summary>High percentile (0..1) for smoothness remap window.</summary>
    public float SpecularRemapHighPercentile { get; init; } = 0.98f;

    /// <summary>
    /// When true, LabPBR specular alpha is forced to 255 (no emission) for every pixel, overriding heuristic rules and ML output.
    /// </summary>
    public bool SpecularForceNoEmissive { get; init; }

    /// <summary>
    /// When true, process block/textures (block, blocks folders).
    /// </summary>
    public bool ProcessBlocks { get; init; } = true;

    /// <summary>
    /// When true, process item textures (item, items folders).
    /// </summary>
    public bool ProcessItems { get; init; } = true;

    /// <summary>
    /// When true, process armor/entity textures (entity folder).
    /// </summary>
    public bool ProcessArmor { get; init; } = true;

    /// <summary>
    /// When true, process particle textures (particle folder). Particles get specular only (no normal/height).
    /// </summary>
    public bool ProcessParticles { get; init; } = true;

    /// <summary>
    /// When true, bake ambient occlusion into the normal map blue channel (LabPBR: 0 = 100% occlusion, 255 = 0% occlusion).
    /// </summary>
    public bool GenerateAo { get; init; }

    /// <summary>AO blur radius (pixels) used for cavity-style approximation.</summary>
    public int AoRadius { get; init; } = 4;

    /// <summary>AO strength multiplier.</summary>
    public float AoStrength { get; init; } = 1f;

    /// <summary>
    /// Keys like "\block\stone" (no extension). If a texture's key matches, it is skipped.
    /// </summary>
    public ISet<string> IgnoreTextureKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Material tag definitions (keywords). Used by <see cref="TextureScanner"/> for organic-tag detection
    /// (<see cref="FoliageMode"/> "No Height" / "Ignore All"). When null or empty,
    /// <see cref="TagRulePresets.Default"/> is used.
    /// </summary>
    public IReadOnlyList<TagRule>? TagRules { get; init; }

    /// <summary>
    /// Optional semantic ML options (MiniLM) used at scan time for organic-tag detection.
    /// When non-null with <see cref="MaterialTagSemanticOptions.Enabled"/> = true, the scanner uses
    /// ML-based material matching instead of keyword-only matching for foliage/organic handling.
    /// </summary>
    public MaterialTagSemanticOptions? SemanticOptions { get; init; }

    /// <summary>
    /// Per-texture-key manual tag add/remove from the explorer. Key = RelativeKey (e.g. \minecraft\block\stone_brick).
    /// When non-null, effective tags = (auto \ removed) ∪ added.
    /// </summary>
    public IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? ManualTagOverrides { get; init; }

    /// <summary>Foliage handling for 2D Sprite–tagged textures only: "Ignore All", "No Height", or "Convert All".</summary>
    public string FoliageMode { get; init; } = "No Height";

    /// <summary>When true and DeepBumpModelPath is valid, generate normals from diffuse using the DeepBump ONNX model (deepbump256.onnx) instead of Sobel/VC.</summary>
    public bool UseDeepBumpNormals { get; init; }

    /// <summary>Path to deepbump256.onnx (from https://github.com/HugoTini/DeepBump). Used when UseDeepBumpNormals is true.</summary>
    public string? DeepBumpModelPath { get; init; }

    /// <summary>DeepBump tile overlap: "Small", "Medium", or "Large". Matches DeepBump --color_to_normals-overlap (default LARGE = best quality).</summary>
    public string DeepBumpOverlap { get; init; } = "Large";

    /// <summary>
    /// DeepBump input mode. Auto uses the ONNX model's declared input channels to choose an appropriate mode.
    /// </summary>
    public DeepBumpInputMode DeepBumpInputMode { get; init; } = DeepBumpInputMode.Auto;

    /// <summary>
    /// When true, force DeepBump output B channel to 255 for workflows expecting "RG + height-in-alpha" style packing.
    /// </summary>
    public bool DeepBumpForceBlue255 { get; init; }

    /// <summary>
    /// DeepBump-only normal strength multiplier applied after ONNX output. 1 = unchanged.
    /// </summary>
    public float DeepBumpNormalIntensity { get; init; } = AutoPBRDefaults.DefaultNormalIntensity;

    /// <summary>
    /// Optional soft clamp (0..2) applied to DeepBump normal XY magnitude after strength scaling.
    /// 0 = linear behavior; higher values preserve punch while reducing near-90 degree saturation.
    /// </summary>
    public float DeepBumpNormalSoftClamp { get; init; }

    /// <summary>
    /// When true, blend DeepBump normals with diffuse-derived edge guidance so strength follows image structure
    /// similarly to heuristic Sobel/Scharr normals.
    /// </summary>
    public bool DeepBumpEdgeGuidedEnhance { get; init; }

    /// <summary>Edge-guided XY magnitude gain (0..6 typical). Higher values amplify edges more strongly.</summary>
    public float DeepBumpEdgeGuidedStrength { get; init; } = 1f;

    /// <summary>Gamma for edge-guidance weighting (0.5..3 typical). Higher values bias enhancement toward strongest edges.</summary>
    public float DeepBumpEdgeGuidedGamma { get; init; } = 1f;

    /// <summary>
    /// Direction blend factor toward diffuse-derived gradient direction (0..1).
    /// 0 = keep model direction; 1 = fully align to edge direction where edges are strong.
    /// </summary>
    public float DeepBumpEdgeGuidedDirectionMix { get; init; } = 0.35f;

    /// <summary>
    /// When true, diffuse pixels with alpha less than or equal to <see cref="NormalHeightTransparentAlphaClampMax"/>
    /// are hard-clamped to RGBA 0 in generated normal/height output (_n).
    /// </summary>
    public bool NormalHeightZeroTransparentPixels { get; init; } = true;

    /// <summary>
    /// Alpha threshold (0..255) for transparent-pixel clamp when <see cref="NormalHeightZeroTransparentPixels"/> is true.
    /// 0 means only fully transparent pixels; values like 4-16 also clamp anti-aliased fringes.
    /// </summary>
    public int NormalHeightTransparentAlphaClampMax { get; init; }

    /// <summary>
    /// When true, register the ONNX Runtime TensorRT execution provider (with CUDA fallback) for GPU sessions.
    /// When false (default), use CUDA only—faster session startup; TensorRT compiles engines on first use.
    /// </summary>
    public bool PreferOnnxTensorRtExecutionProvider { get; init; }

    /// <summary>
    /// When true, use ONNX direct specular predictor (diffuse → RGBA) before heuristic/class paths.
    /// </summary>
    public bool UseMlSpecularPredictor { get; init; }

    /// <summary>Path to direct specular ONNX model (fallback when <see cref="MlSpecularModelPathsByResolution"/> is null or empty).</summary>
    public string? MlSpecularModelPath { get; init; }

    /// <summary>
    /// Optional per-texture-resolution paths to specular ONNX models (edge length in pixels, e.g. 16, 32, 64, 128, 256).
    /// When non-empty, selection uses <b>ceil</b>: smallest configured resolution &gt;= texture size; if texture is larger than all keys, the largest key is used.
    /// Invalid entries (non-positive keys, empty paths) are ignored. When null or empty, <see cref="MlSpecularModelPath"/> alone is used.
    /// </summary>
    public IReadOnlyDictionary<int, string>? MlSpecularModelPathsByResolution { get; init; }

    /// <summary>
    /// 0 = heuristic-only specular when ML inference ran; 1 = full ML contribution (linear blend per pixel between
    /// heuristic and model; which channels follow <see cref="MlSpecularHeuristicBlendMode"/>).
    /// </summary>
    public float MlSpecularHeuristicBlend { get; init; } = AutoPBRDefaults.DefaultMlSpecularHeuristicBlend;

    /// <summary>
    /// Which channels receive heuristic contribution when <see cref="MlSpecularHeuristicBlend"/> &gt; 0.
    /// </summary>
    public MlSpecularHeuristicBlendMode MlSpecularHeuristicBlendMode { get; init; } =
        AutoPBRDefaults.DefaultMlSpecularHeuristicBlendMode;

    /// <summary>
    /// Blend math used when combining heuristic and ML for channels selected by <see cref="MlSpecularHeuristicBlendMode"/>.
    /// </summary>
    public MlSpecularBlendMath MlSpecularBlendMath { get; init; } = AutoPBRDefaults.DefaultMlSpecularBlendMath;

    /// <summary>When true, append edge magnitude as 4th input channel if direct spec model expects it.</summary>
    public bool MlSpecularUseEdgeChannel { get; init; } = true;

    /// <summary>
    /// When true and ML inference ran, per-texture smoothness (R) remap uses only non-ML pixels for min/max (or percentiles)
    /// and is applied only to non-ML pixels — model R stays as predicted. Heuristic pixels keep the usual remap. When false,
    /// behavior matches older builds (entire texture remapped together). Ignored when <see cref="SpecularDebugSkipSpecularRemap"/> is true.
    /// </summary>
    public bool MlSpecularSkipSmoothnessRemap { get; init; } = true;

    /// <summary>
    /// When true, diffuse pixels with alpha less than or equal to <see cref="MlSpecularTransparentAlphaClampMax"/> are hard-clamped to RGBA 0
    /// in generated _s output. This suppresses model hallucinations in empty/near-empty sprite regions.
    /// </summary>
    public bool MlSpecularZeroTransparentPixels { get; init; } = true;

    /// <summary>
    /// Alpha threshold (0..255) for transparent-pixel clamp when <see cref="MlSpecularZeroTransparentPixels"/> is true.
    /// 0 means only fully transparent pixels; values like 4-16 also clamp anti-aliased fringes.
    /// </summary>
    public int MlSpecularTransparentAlphaClampMax { get; init; }

    /// <summary>
    /// Debug: when ML specular is enabled, never use the heuristic/tag specular path. Pixels that would fall back
    /// (or the whole texture if inference fails) are filled with magenta (255,0,255) so they are obvious in previews.
    /// </summary>
    public bool SpecularDebugDisableHeuristicSpecular { get; init; }

    /// <summary>Debug: skip per-texture smoothness (R) percentile/min-max remap so _s matches model output more closely.</summary>
    public bool SpecularDebugSkipSpecularRemap { get; init; }

    /// <summary>Debug: log extra specular-ML diagnostics (load errors, tensor shapes, first-pixel sample) per texture.</summary>
    public bool SpecularDebugVerboseSpecularMl { get; init; }

    /// <summary>
    /// When true and the texture has the <c>brick</c> material tag, apply structural mortar detection and height post-processing.
    /// </summary>
    public bool BrickHeightMapPostProcessEnabled { get; init; } = AutoPBRDefaults.DefaultBrickHeightMapPostProcessEnabled;

    /// <summary>With <see cref="BrickHeightInvertConfidenceFloor"/> sets the minimum mean structural response for the strong global invert path.</summary>
    public float BrickHeightMinStructuralConfidence { get; init; } = AutoPBRDefaults.DefaultBrickHeightMinStructuralConfidence;

    /// <summary>Mean mortar response must reach this for the strong Δ-only global invert path.</summary>
    public float BrickHeightInvertConfidenceFloor { get; init; } = AutoPBRDefaults.DefaultBrickHeightInvertConfidenceFloor;

    /// <summary>Mortar mean height minus brick mean height (0–255) above this may trigger global invert on the strong path.</summary>
    public float BrickHeightInvertDeltaThreshold { get; init; } = AutoPBRDefaults.DefaultBrickHeightInvertDeltaThreshold;

    /// <summary>
    /// Minimum weighted diffuse luminance gap (mortar minus brick, 0–1) to allow the light-grout global invert path when Δ &gt; 0. Set to a large value (e.g. 1) to disable.
    /// </summary>
    public float BrickLightGroutDiffuseDeltaMin { get; init; } = AutoPBRDefaults.DefaultBrickLightGroutDiffuseDeltaMin;

    /// <summary>Local depression: <c>H *= (1 - alpha * S)</c> for soft mortar mask S.</summary>
    public float BrickMortarDepressionAlpha { get; init; } = AutoPBRDefaults.DefaultBrickMortarDepressionAlpha;

    /// <summary>Lift added proportionally on bulk brick areas after depression (0–255 scale).</summary>
    public float BrickBulkLiftBeta { get; init; } = AutoPBRDefaults.DefaultBrickBulkLiftBeta;

    /// <summary>Maximum morphological radius for multi-scale top-hat (actual radii also scale with texture size).</summary>
    public int BrickMortarTopHatMaxRadius { get; init; } = AutoPBRDefaults.DefaultBrickMortarTopHatMaxRadius;

    /// <summary>When true, append brick height probe summary to conversion progress <see cref="ConversionProgress.InfoMessage"/>.</summary>
    public bool BrickHeightMapVerboseLog { get; init; }

    /// <summary>When true, brick height probe fills <see cref="TextureWorkItem.BrickProbeDebugText"/> during preview (single-texture path).</summary>
    public bool BrickProbePreviewDebug { get; init; } = AutoPBRDefaults.DefaultBrickProbePreviewDebug;

    /// <summary>
    /// When true, invert specular smoothness (R) for <c>brick</c>-tagged textures using the same global invert decision as brick height
    /// (requires normals/height to run before specular). When false, use legacy <see cref="TextureOverrides.InvertSpecular"/> for brick.
    /// </summary>
    public bool BrickSpecularAlignWithHeightProbe { get; init; } = AutoPBRDefaults.DefaultBrickSpecularAlignWithHeightProbe;

    public SpecularData? SpecularData { get; init; }
}

/// <summary>How ML specular is mixed with heuristic output when both are available.</summary>
public enum MlSpecularHeuristicBlendMode
{
    /// <summary>
    /// Only smoothness (R) mixes heuristic vs ML. Metallic (G), porosity (B), and emissive (A) use the model only
    /// (heuristic does not contribute hg/hb/ha in the blend). When blend is 0, all channels match heuristics.
    /// </summary>
    SmoothnessOnly = 0,

    /// <summary>Heuristic contributes to every channel: R, G, B, and A each lerp between heuristic and ML.</summary>
    Full = 1,

    /// <summary>
    /// Same as <see cref="SmoothnessOnly"/> for R/G/A: only R blends heuristic↔ML while G/A come from ML.
    /// B (porosity) stays heuristic.
    /// </summary>
    AiMetalAndEmissive = 2
}

/// <summary>Math function for heuristic↔ML channel mixing.</summary>
public enum MlSpecularBlendMath
{
    /// <summary>Linear interpolation: <c>(1-mix)*heuristic + mix*ml</c>.</summary>
    Linear = 0,

    /// <summary>
    /// Soft-light composite target with heuristic-preserving crossfade:
    /// <c>lerp(heuristic, softLight(heuristic, ml), mix)</c>.
    /// </summary>
    SoftLight = 1,

    /// <summary>
    /// Overlay composite target with heuristic-preserving crossfade:
    /// <c>lerp(heuristic, overlay(heuristic, ml), mix)</c>.
    /// </summary>
    Overlay = 2,

    /// <summary>
    /// Screen composite target with heuristic-preserving crossfade:
    /// <c>lerp(heuristic, screen(heuristic, ml), mix)</c>.
    /// </summary>
    Screen = 3,

    /// <summary>
    /// Gain-curve remap (controlled by ML) with heuristic-preserving crossfade:
    /// <c>lerp(heuristic, gain(heuristic, ml), mix)</c>.
    /// </summary>
    BiasGain = 4,

    /// <summary>
    /// Logit-space interpolation between heuristic and ML:
    /// <c>sigmoid(lerp(logit(heuristic), logit(ml), mix))</c>.
    /// </summary>
    SigmoidCrossfade = 5
}

public enum NormalOperator
{
    SobelVc,
    ScharrVc
}

public enum NormalKernelSize
{
    K3 = 3,
    K5 = 5,
    K7 = 7
}

public enum NormalDerivative
{
    Luminance,
    Color,
    ColorLuminanceBlend,
    ColorLuminanceMax
}

public enum DeepBumpInputMode
{
    Auto,
    Grayscale,
    Rgb
}



