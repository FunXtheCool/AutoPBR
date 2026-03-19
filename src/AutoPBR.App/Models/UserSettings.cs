using System.Text.Json;
using System.Text.Json.Serialization;
using AutoPBR.Core;
using AutoPBR.Core.Models;
using NormalOperatorEnum = AutoPBR.Core.Models.NormalOperator;
using NormalDerivativeEnum = AutoPBR.Core.Models.NormalDerivative;
using QualityProfileEnum = AutoPBR.Core.Models.QualityProfile;
using DeepBumpInputModeEnum = AutoPBR.Core.Models.DeepBumpInputMode;

namespace AutoPBR.App.Models;

public sealed class UserSettings
{
    public string? OutputDirectory { get; set; }

    /// <summary>Folder scanned for batch .zip / .jar resource packs (Scan tab).</summary>
    public string? BatchFolderPath { get; set; }
    public double NormalIntensity { get; set; } = AutoPbrDefaults.DefaultNormalIntensity;
    public double HeightIntensity { get; set; } = AutoPbrDefaults.DefaultHeightIntensity;
    public bool FastSpecular { get; set; }
    public string FoliageMode { get; set; } = "Ignore All";

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
    public int MaxThreads { get; set; } // 0 = auto
    public string? TempDirectory { get; set; }
    public string ColorScheme { get; set; } = "Dark";

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

    /// <summary>Normal operator when not using DeepBump. \"SobelVc\" or \"ScharrVc\".</summary>
    public string NormalOperator { get; set; } = nameof(NormalOperatorEnum.SobelVc);

    /// <summary>Normal kernel size for Sobel/Scharr when not using DeepBump. \"3\", \"5\", or \"7\" (7 only for Sobel).</summary>
    public string NormalKernelSize { get; set; } = "3";

    /// <summary>What to derive normals from: Luminance, Color, ColorLuminanceBlend, or ColorLuminanceMax.</summary>
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

    /// <summary>User-defined tag rules (keywords + overrides). Applied in addition to built-in rules.</summary>
    public List<CustomTagRuleEntry> CustomTagRules { get; set; } = [];

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

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
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort; ignore persistence errors.
        }
    }
}

