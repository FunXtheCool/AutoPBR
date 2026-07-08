using System.Text.Json;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

static int Usage()
{
    Console.WriteLine(
        """
        AutoPBR.Cli

        Usage:
          AutoPBR.Cli <input> <output> [--fast] [--normal <1..3>] [--height <0.01..0.5>] [--normal-operator <sobel|scharr>] [--normal-kernel <3|5|7>] [--normal-derivative <luminance|color|blend|max>] [--foliage-mode <ignore-all|no-height|convert-all>] [--ignore-plants] [--no-semantic-material-tags] [--semantic-min-similarity <0..1>] [--semantic-certainty-threshold <0..1>] [--semantic-max-tags <1..16>] [--dictionary-evidence] [--dictionary-evidence-weight <0..1>] [--dictionary-min-evidence-score <-1..1>] [--dictionary-timeout-ms <100..5000>] [--tag-rules <path.json>] [--deepbump-normal-strength <0.05..8>] [--deepbump-normal-soft-clamp <0..2>] [--deepbump-edge-guided] [--deepbump-edge-strength <0..6>] [--deepbump-edge-gamma <0.1..8>] [--deepbump-edge-direction-mix <0..1>] [--normal-height-transparent-alpha-max <0..255>] [--ml-spec-model <path.onnx>|--ml-spec-generator-model <path.onnx>|--ml-spec-predictor-model <path.onnx>] [--ml-spec-model-map <res>=<path.onnx> ...] [--ml-spec-blend-math <linear|softlight|overlay|screen|biasgain|sigmoidcrossfade>] [--ml-spec-no-edge] [--ml-spec-transparent-alpha-max <0..255>] [--debug-spec-no-heuristic] [--debug-spec-no-remap] [--debug-spec-verbose]

        Input: .zip (resource pack) or .jar (Minecraft; opened as zip). Output: always .zip (PBR layer only).

        Notes:
          - Specular lookup data is loaded from: <app>/Data/textures_data.json
          - Tag rules / MiniLM / weighted-unweighted flags match the desktop app defaults (see --foliage-mode, semantic flags).
          - --foliage-mode defaults to no-height (same as app). --ignore-plants is a legacy alias for ignore-all (does not skip textures by path).
          - --tag-rules: JSON array of custom tag rules (same shape as app export); merged after built-in brick/wood/metal/foliage rules.
          - Specular ML: use --ml-spec-model for a single fallback ONNX, and/or repeat --ml-spec-model-map <res>=<path> (e.g. 16=Data\\ONNX-AI\\SpecLab\\SpecLab_16x.onnx). If neither is set, bundled models under Data/ONNX-AI/SpecLab are auto-discovered when present. Per-texture selection picks the smallest configured resolution >= texture size (ceil), else the largest.
        """
    );
    return 2;
}

if (args.Length < 2)
{
    return Usage();
}

var input = args[0];
var output = args[1];

var fast = args.Any(a => a.Equals("--fast", StringComparison.OrdinalIgnoreCase));
float normal = AutoPBRDefaults.DefaultNormalIntensity;
float height = AutoPBRDefaults.DefaultHeightIntensity;
var normalOperator = NormalOperator.SobelVc;
var normalKernelSize = NormalKernelSize.K3;
var normalDerivative = NormalDerivative.Luminance;
float deepBumpNormalStrength = AutoPBRDefaults.DefaultNormalIntensity;
float deepBumpNormalSoftClamp = 0f;
var deepBumpEdgeGuided = args.Any(a => a.Equals("--deepbump-edge-guided", StringComparison.OrdinalIgnoreCase));
float deepBumpEdgeStrength = 1f;
float deepBumpEdgeGamma = 1f;
float deepBumpEdgeDirectionMix = 0.35f;
var normalHeightTransparentAlphaClampMax = 0;
var ignorePlantsLegacy = args.Any(a => a.Equals("--ignore-plants", StringComparison.OrdinalIgnoreCase));
string? foliageModeArg = null;
var useSemanticMaterialTags = !args.Any(a => a.Equals("--no-semantic-material-tags", StringComparison.OrdinalIgnoreCase));
double semanticMinSimilarity = 0.25;
double semanticCertaintyThreshold = 0.35;
int semanticMaxTags = 3;
var dictionaryEvidence = args.Any(a => a.Equals("--dictionary-evidence", StringComparison.OrdinalIgnoreCase));
double dictionaryEvidenceWeight = 0.35;
double dictionaryMinEvidenceScore = 0.18;
int dictionaryRequestTimeoutMs = 900;
string? tagRulesJsonPath = null;
string? mlSpecModelPath = null;
var mlSpecModelMap = new Dictionary<int, string>();
var mlSpecBlendMath = MlSpecularBlendMath.Linear;
var mlSpecUseEdgeChannel = !args.Any(a => a.Equals("--ml-spec-no-edge", StringComparison.OrdinalIgnoreCase));
var mlSpecTransparentAlphaClampMax = 0;
var debugSpecNoHeuristic = args.Any(a => a.Equals("--debug-spec-no-heuristic", StringComparison.OrdinalIgnoreCase));
var debugSpecNoRemap = args.Any(a => a.Equals("--debug-spec-no-remap", StringComparison.OrdinalIgnoreCase));
var debugSpecVerbose = args.Any(a => a.Equals("--debug-spec-verbose", StringComparison.OrdinalIgnoreCase));

for (var i = 2; i < args.Length; i++)
{
    if (args[i].Equals("--normal", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var n))
    {
        normal = n;
    }

    if (args[i].Equals("--height", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var h))
    {
        height = h;
    }

    if (args[i].Equals("--normal-operator", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        var val = args[i + 1];
        if (val.Equals("sobel", StringComparison.OrdinalIgnoreCase))
        {
            normalOperator = NormalOperator.SobelVc;
        }
        else if (val.Equals("scharr", StringComparison.OrdinalIgnoreCase))
        {
            normalOperator = NormalOperator.ScharrVc;
        }
        else
        {
            Console.Error.WriteLine("Invalid value for --normal-operator. Expected 'sobel' or 'scharr'.");
            return 2;
        }
    }

    if (args[i].Equals("--normal-kernel", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        var val = args[i + 1];
        if (val is "3" or "3x3")
        {
            normalKernelSize = NormalKernelSize.K3;
        }
        else if (val is "5" or "5x5")
        {
            normalKernelSize = NormalKernelSize.K5;
        }
        else if (val is "7" or "7x7")
        {
            normalKernelSize = NormalKernelSize.K7;
        }
        else
        {
            Console.Error.WriteLine("Invalid value for --normal-kernel. Expected 3, 5, or 7.");
            return 2;
        }

        if (normalOperator == NormalOperator.ScharrVc && normalKernelSize == NormalKernelSize.K7)
        {
            Console.Error.WriteLine("Scharr supports kernel sizes 3 or 5 only (7x7 is invalid).");
            return 2;
        }
    }

    if (args[i].Equals("--normal-derivative", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        var val = args[i + 1];
        if (val.Equals("luminance", StringComparison.OrdinalIgnoreCase))
        {
            normalDerivative = NormalDerivative.Luminance;
        }
        else if (val.Equals("color", StringComparison.OrdinalIgnoreCase))
        {
            normalDerivative = NormalDerivative.Color;
        }
        else if (val.Equals("blend", StringComparison.OrdinalIgnoreCase))
        {
            normalDerivative = NormalDerivative.ColorLuminanceBlend;
        }
        else if (val.Equals("max", StringComparison.OrdinalIgnoreCase))
        {
            normalDerivative = NormalDerivative.ColorLuminanceMax;
        }
        else
        {
            Console.Error.WriteLine(
                "Invalid value for --normal-derivative. Expected 'luminance', 'color', 'blend', or 'max'.");
            return 2;
        }
    }

    if (args[i].Equals("--foliage-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        foliageModeArg = args[i + 1];
    }

    if (args[i].Equals("--semantic-min-similarity", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var sms))
    {
        semanticMinSimilarity = sms;
    }

    if (args[i].Equals("--semantic-certainty-threshold", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var sct))
    {
        semanticCertaintyThreshold = sct;
    }

    if (args[i].Equals("--semantic-max-tags", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        int.TryParse(args[i + 1], out var smt))
    {
        semanticMaxTags = smt;
    }

    if (args[i].Equals("--dictionary-evidence-weight", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var dew))
    {
        dictionaryEvidenceWeight = dew;
    }

    if (args[i].Equals("--dictionary-min-evidence-score", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var dmes))
    {
        dictionaryMinEvidenceScore = dmes;
    }

    if (args[i].Equals("--dictionary-timeout-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        int.TryParse(args[i + 1], out var dtm))
    {
        dictionaryRequestTimeoutMs = dtm;
    }

    if (args[i].Equals("--tag-rules", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        tagRulesJsonPath = args[i + 1];
    }

    if (args[i].Equals("--deepbump-normal-strength", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var dbn))
    {
        deepBumpNormalStrength = Math.Clamp(dbn, 0.05f, 8f);
    }
    if (args[i].Equals("--deepbump-normal-soft-clamp", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var dbc))
    {
        deepBumpNormalSoftClamp = Math.Clamp(dbc, 0f, 2f);
    }
    if (args[i].Equals("--deepbump-edge-strength", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var des))
    {
        deepBumpEdgeStrength = Math.Clamp(des, 0f, 6f);
    }
    if (args[i].Equals("--deepbump-edge-gamma", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var deg))
    {
        deepBumpEdgeGamma = Math.Clamp(deg, 0.1f, 8f);
    }
    if (args[i].Equals("--deepbump-edge-direction-mix", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        float.TryParse(args[i + 1], out var dem))
    {
        deepBumpEdgeDirectionMix = Math.Clamp(dem, 0f, 1f);
    }
    if (args[i].Equals("--normal-height-transparent-alpha-max", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        int.TryParse(args[i + 1], out var normalHeightAlphaMax))
    {
        normalHeightTransparentAlphaClampMax = Math.Clamp(normalHeightAlphaMax, 0, 255);
    }

    if (args[i].Equals("--ml-spec-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        mlSpecModelPath = args[i + 1];
    }
    if (args[i].Equals("--ml-spec-generator-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        mlSpecModelPath = args[i + 1];
    }
    if (args[i].Equals("--ml-spec-predictor-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        mlSpecModelPath = args[i + 1];
    }

    if (args[i].Equals("--ml-spec-model-map", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        var entry = args[i + 1];
        if (!MlSpecularModelMapParsing.TryParseMapEntry(entry, out var res, out var mapPath, out var mapErr))
        {
            Console.Error.WriteLine($"Invalid --ml-spec-model-map: {mapErr}");
            return 2;
        }

        mlSpecModelMap[res] = mapPath;
    }

    if (args[i].Equals("--ml-spec-blend-math", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        if (!TryParseMlSpecBlendMathToken(args[i + 1], out var math))
        {
            Console.Error.WriteLine(
                "Invalid --ml-spec-blend-math. Expected linear, softlight, overlay, screen, biasgain, or sigmoidcrossfade.");
            return 2;
        }

        mlSpecBlendMath = math;
    }

    if (args[i].Equals("--ml-spec-transparent-alpha-max", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
        int.TryParse(args[i + 1], out var alphaMax))
    {
        mlSpecTransparentAlphaClampMax = Math.Clamp(alphaMax, 0, 255);
    }

}

IReadOnlyDictionary<int, string>? mlSpecularPerResolution = null;
if (mlSpecModelMap.Count > 0)
{
    mlSpecularPerResolution = mlSpecModelMap;
}
else if (string.IsNullOrWhiteSpace(mlSpecModelPath))
{
    mlSpecularPerResolution = MlSpecularBundledModelPaths.TryBuildMapFromBundledFiles(AppContext.BaseDirectory);
}

var useMlSpecularPredictor = !string.IsNullOrWhiteSpace(mlSpecModelPath)
    || (mlSpecularPerResolution is not null && mlSpecularPerResolution.Count > 0);

if (foliageModeArg is not null && !TryParseFoliageModeToken(foliageModeArg, out _))
{
    Console.Error.WriteLine(
        "Invalid --foliage-mode. Use ignore-all, no-height, or convert-all (or quoted \"Ignore All\", \"No Height\", \"Convert All\").");
    return 2;
}

var foliageMode = ResolveFoliageMode(foliageModeArg, ignorePlantsLegacy);

var semanticOptions = BuildCliSemanticOptions(
    useSemanticMaterialTags,
    semanticMinSimilarity,
    semanticCertaintyThreshold,
    semanticMaxTags,
    dictionaryEvidence,
    dictionaryEvidenceWeight,
    dictionaryMinEvidenceScore,
    dictionaryRequestTimeoutMs);

var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json");
if (!File.Exists(dataPath))
{
    Console.Error.WriteLine($"Missing specular data file: {dataPath}");
    return 1;
}

IReadOnlyList<TagRule>? tagRules = null;
if (!string.IsNullOrWhiteSpace(tagRulesJsonPath))
{
    if (!File.Exists(tagRulesJsonPath))
    {
        Console.Error.WriteLine($"Tag rules file not found: {tagRulesJsonPath}");
        return 2;
    }

    try
    {
        var json = File.ReadAllText(tagRulesJsonPath);
        var extra = JsonSerializer.Deserialize<List<CustomTagRuleEntry>>(json);
        var appended = extra?
            .Where(e => !string.IsNullOrWhiteSpace(e.Id))
            .Select(e => e.ToTagRule())
            .ToList() ?? [];
        var merged = new List<TagRule>(TagRulePresets.Default);
        merged.AddRange(appended);
        tagRules = merged;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load --tag-rules: {ex.Message}");
        return 2;
    }
}

var options = new AutoPBROptions
{
    FastSpecular = fast,
    NormalIntensity = normal,
    HeightIntensity = height,
    NormalOperator = normalOperator,
    NormalKernelSize = normalKernelSize,
    NormalDerivative = normalDerivative,
    DeepBumpNormalIntensity = deepBumpNormalStrength,
    DeepBumpNormalSoftClamp = deepBumpNormalSoftClamp,
    DeepBumpEdgeGuidedEnhance = deepBumpEdgeGuided,
    DeepBumpEdgeGuidedStrength = deepBumpEdgeStrength,
    DeepBumpEdgeGuidedGamma = deepBumpEdgeGamma,
    DeepBumpEdgeGuidedDirectionMix = deepBumpEdgeDirectionMix,
    NormalHeightTransparentAlphaClampMax = normalHeightTransparentAlphaClampMax,
    SpecularData = SpecularData.LoadFromFile(dataPath),
    FoliageMode = foliageMode,
    IgnoreTextureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    TagRules = tagRules,
    SemanticOptions = semanticOptions,
    UseMlSpecularPredictor = useMlSpecularPredictor,
    MlSpecularModelPath = string.IsNullOrWhiteSpace(mlSpecModelPath) ? null : mlSpecModelPath,
    MlSpecularModelPathsByResolution = mlSpecularPerResolution is { Count: > 0 } ? mlSpecularPerResolution : null,
    MlSpecularBlendMath = mlSpecBlendMath,
    MlSpecularUseEdgeChannel = mlSpecUseEdgeChannel,
    MlSpecularTransparentAlphaClampMax = mlSpecTransparentAlphaClampMax,
    SpecularDebugDisableHeuristicSpecular = debugSpecNoHeuristic,
    SpecularDebugSkipSpecularRemap = debugSpecNoRemap,
    SpecularDebugVerboseSpecularMl = debugSpecVerbose
};

var prog = new Progress<ConversionProgress>(p =>
{
    if (!string.IsNullOrEmpty(p.InfoMessage))
    {
        Console.WriteLine(p.InfoMessage);
    }

    if (p.Stage is ConversionStage.Extracting or ConversionStage.Packing or ConversionStage.Done)
    {
        Console.WriteLine(p.Stage);
    }
    else
    {
        Console.WriteLine($"{p.Stage} {p.Completed}/{p.Total} {p.CurrentTextureName}");
    }
});

try
{
    await ResourcePackConverter.ConvertAsync(input, output, options, prog);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}

static bool TryParseFoliageModeToken(string raw, out string canonical)
{
    canonical = "";
    var t = raw.Trim();
    if (t.Length == 0)
    {
        return false;
    }

    var compact = t.Replace("-", "", StringComparison.OrdinalIgnoreCase)
        .Replace(" ", "", StringComparison.OrdinalIgnoreCase);

    if (compact.Equals("ignoreall", StringComparison.OrdinalIgnoreCase))
    {
        canonical = "Ignore All";
        return true;
    }

    if (compact.Equals("noheight", StringComparison.OrdinalIgnoreCase))
    {
        canonical = "No Height";
        return true;
    }

    if (compact.Equals("convertall", StringComparison.OrdinalIgnoreCase))
    {
        canonical = "Convert All";
        return true;
    }

    if (t.Equals("Ignore All", StringComparison.OrdinalIgnoreCase) ||
        t.Equals("No Height", StringComparison.OrdinalIgnoreCase) ||
        t.Equals("Convert All", StringComparison.OrdinalIgnoreCase))
    {
        canonical = t switch
        {
            _ when t.Equals("Ignore All", StringComparison.OrdinalIgnoreCase) => "Ignore All",
            _ when t.Equals("No Height", StringComparison.OrdinalIgnoreCase) => "No Height",
            _ => "Convert All"
        };
        return true;
    }

    return false;
}

static string ResolveFoliageMode(string? foliageModeArg, bool ignorePlantsLegacy)
{
    if (foliageModeArg is not null && TryParseFoliageModeToken(foliageModeArg, out var c))
    {
        return c;
    }

    if (ignorePlantsLegacy)
    {
        return "Ignore All";
    }

    return "No Height";
}

static bool TryParseMlSpecBlendMathToken(string raw, out MlSpecularBlendMath value)
{
    value = MlSpecularBlendMath.Linear;
    var t = raw.Trim().ToLowerInvariant().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    value = t switch
    {
        "linear" => MlSpecularBlendMath.Linear,
        "softlight" => MlSpecularBlendMath.SoftLight,
        "overlay" => MlSpecularBlendMath.Overlay,
        "screen" => MlSpecularBlendMath.Screen,
        "biasgain" => MlSpecularBlendMath.BiasGain,
        "sigmoidcrossfade" => MlSpecularBlendMath.SigmoidCrossfade,
        _ => value
    };
    return t is "linear" or "softlight" or "overlay" or "screen" or "biasgain" or "sigmoidcrossfade";
}

static MaterialTagSemanticOptions? BuildCliSemanticOptions(
    bool useSemantic,
    double minSimilarity,
    double certaintyThreshold,
    int maxTags,
    bool dictionaryEvidenceEnabled,
    double dictionaryEvidenceWeight,
    double dictionaryMinEvidenceScore,
    int dictionaryRequestTimeoutMs)
{
    if (!useSemantic)
    {
        return null;
    }

    var matcher = MaterialTagSemanticMatcher.TryCreate(AppContext.BaseDirectory);
    if (matcher is null)
    {
        return null;
    }

    return new MaterialTagSemanticOptions
    {
        Enabled = true,
        MinSimilarity = Math.Clamp(minSimilarity, 0.05, 0.99),
        CertaintyThreshold = Math.Clamp(certaintyThreshold, 0.05, 0.99),
        MaxTags = Math.Clamp(maxTags, 1, 16),
        Matcher = matcher,
        DictionaryEvidenceEnabled = dictionaryEvidenceEnabled,
        DictionaryEvidenceWeight = Math.Clamp(dictionaryEvidenceWeight, 0.0, 1.0),
        DictionaryMinEvidenceScore = Math.Clamp(dictionaryMinEvidenceScore, -1.0, 1.0),
        DictionaryRequestTimeoutMs = Math.Clamp(dictionaryRequestTimeoutMs, 100, 5000),
        DictionaryProvider = new FreeDictionaryDefinitionProvider()
    };
}
