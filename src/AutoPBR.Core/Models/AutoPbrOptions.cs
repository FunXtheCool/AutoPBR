namespace AutoPBR.Core.Models;

public sealed class AutoPbrOptions
{
    /// <summary>
    /// Optional preset that can be used to tune defaults for different target resolutions.
    /// When set, higher layers (UI/CLI) may apply recommended settings for the selected profile.
    /// </summary>
    public QualityProfile QualityProfile { get; init; } = QualityProfile.Balanced;

    public float NormalIntensity { get; init; } = AutoPbrDefaults.DefaultNormalIntensity;
    public float HeightIntensity { get; init; } = AutoPbrDefaults.DefaultHeightIntensity;
    public bool FastSpecular { get; init; } = false;

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
    public bool PreprocessLinearize { get; init; } = false;

    /// <summary>
    /// Optional denoise radius applied to derived luminance before gradients (0 = off). Low-res friendly.
    /// </summary>
    public int PreprocessDenoiseRadius { get; init; } = 0;

    /// <summary>
    /// Blend factor for denoise (0 = keep original, 1 = fully denoised). Only used when radius &gt; 0.
    /// </summary>
    public float PreprocessDenoiseBlend { get; init; } = 0.5f;

    /// <summary>
    /// When true, apply a simple frequency-split to luminance before gradients: low-pass + (high-pass * strength).
    /// </summary>
    public bool PreprocessFrequencySplit { get; init; } = false;

    /// <summary>Radius for the low-pass used by frequency split (pixels).</summary>
    public int PreprocessFrequencyRadius { get; init; } = 2;

    /// <summary>High-frequency contribution multiplier for frequency split.</summary>
    public float PreprocessFrequencyDetailStrength { get; init; } = 1f;

    /// <summary>
    /// When true, use the legacy ZipFile-based extractor instead of the default parallel extractor.
    /// Use only if you hit issues with the default (e.g. exotic zip format).
    /// </summary>
    public bool UseLegacyExtractor { get; init; } = false;

    /// <summary>
    /// When non-null and non-empty, only these zip entry paths are extracted (e.g. from a prior scan).
    /// Reduces extraction time and disk use when only .png textures (and pack.mcmeta) are needed.
    /// </summary>
    public IReadOnlyList<string>? EntriesToExtractOnly { get; init; }

    /// <summary>
    /// Maximum worker threads to use for conversion (specular/normal/height). 0 or less = auto (CPU-2, minimum 1).
    /// </summary>
    public int MaxThreads { get; init; } = 0;

    /// <summary>
    /// Optional base directory for temporary working files. When null or empty, the system temp directory is used.
    /// </summary>
    public string? TempDirectory { get; init; }

    /// <summary>Scale for dielectric smoothness (R channel). 1 = unchanged; 0.5–1.5 typical.</summary>
    public float SmoothnessScale { get; init; } = AutoPbrDefaults.DefaultSmoothnessScale;

    /// <summary>Boost for metal smoothness (R channel). 1 = unchanged.</summary>
    public float MetallicBoost { get; init; } = AutoPbrDefaults.DefaultMetallicBoost;

    /// <summary>Offset added to porosity/subsurface (B channel). Can be negative.</summary>
    public int PorosityBias { get; init; } = AutoPbrDefaults.DefaultPorosityBias;

    /// <summary>
    /// When true, normalize per-texture smoothness (R) using percentile remap (more robust than min/max on noisy low-res inputs).
    /// </summary>
    public bool SpecularUsePercentileRemap { get; init; } = true;

    /// <summary>Low percentile (0..1) for smoothness remap window.</summary>
    public float SpecularRemapLowPercentile { get; init; } = 0.02f;

    /// <summary>High percentile (0..1) for smoothness remap window.</summary>
    public float SpecularRemapHighPercentile { get; init; } = 0.98f;

    /// <summary>
    /// Optional list of substrings used by the "metal" heuristic. When null, the built-in list is used.
    /// </summary>
    public IReadOnlyList<string>? MetalHeuristicSubstrings { get; init; }

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
    public bool GenerateAo { get; init; } = false;

    /// <summary>AO blur radius (pixels) used for cavity-style approximation.</summary>
    public int AoRadius { get; init; } = 4;

    /// <summary>AO strength multiplier.</summary>
    public float AoStrength { get; init; } = 1f;

    /// <summary>
    /// Keys like "\block\stone" (no extension). If a texture's key matches, it is skipped.
    /// </summary>
    public ISet<string> IgnoreTextureKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tag rules for keyword-based conversion overrides (e.g. "brick" → invert height + specular).
    /// When null or empty, <see cref="TagRulePresets.Default"/> is used.
    /// </summary>
    public IReadOnlyList<TagRule>? TagRules { get; init; }

    /// <summary>
    /// Per-texture-key manual tag add/remove from the explorer. Key = RelativeKey (e.g. \minecraft\block\stone_brick).
    /// When non-null, effective tags = (auto \ removed) ∪ added before merging overrides.
    /// </summary>
    public IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? ManualTagOverrides { get; init; }

    /// <summary>Foliage handling: "Ignore All", "No Height", or "Convert All".</summary>
    public string FoliageMode { get; init; } = "Ignore All";

    /// <summary>When true and DeepBumpModelPath is valid, generate normals from diffuse using the DeepBump ONNX model (deepbump256.onnx) instead of Sobel/VC.</summary>
    public bool UseDeepBumpNormals { get; init; } = false;

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
    public bool DeepBumpForceBlue255 { get; init; } = false;

    public SpecularData? SpecularData { get; init; }
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

public enum QualityProfile
{
    Balanced,
    LowRes,
    HiRes
}

public enum DeepBumpInputMode
{
    Auto,
    Grayscale,
    Rgb
}



