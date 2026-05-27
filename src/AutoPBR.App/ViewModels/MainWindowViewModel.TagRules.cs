using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JetBrains.Annotations;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels.Rulesets;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private void NotifyTagRulesChanged()
    {
        Rulesets.RefreshPresentation();
        _materialTagSemanticMatcher?.Dispose();
        _materialTagSemanticMatcher = null;
        _exploreTagFilterOptions = null;
        OnPropertyChanged(nameof(ExploreTagFilterOptions));
        _exploreController.RefreshAllDisplayTags();
        if (!_loadingSettings)
        {
            SaveSettings();
        }
    }
    partial void OnUseSemanticMaterialTagsChanged(bool value)
    {
        if (!value)
        {
            _materialTagSemanticMatcher?.Dispose();
            _materialTagSemanticMatcher = null;
        }

        if (!_loadingSettings)
        {
            _exploreController.RefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagMinSimilarityChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagMaxCountChanged(int value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagCertaintyThresholdChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryEvidenceEnabledChanged(bool value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.RefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryEvidenceWeightChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryMinEvidenceScoreChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryRequestTimeoutMsChanged(int value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    private MaterialTagSemanticOptions? BuildMaterialTagSemanticOptions()
    {
        if (!UseSemanticMaterialTags)
        {
            return null;
        }

        _materialTagSemanticMatcher ??= MaterialTagSemanticMatcher.TryCreate();
        if (_materialTagSemanticMatcher is null)
        {
            return null;
        }

        return new MaterialTagSemanticOptions
        {
            Enabled = true,
            MinSimilarity = MaterialTagMinSimilarity,
            CertaintyThreshold = MaterialTagCertaintyThreshold,
            MaxTags = Math.Clamp(MaterialTagMaxCount, 1, 16),
            Matcher = _materialTagSemanticMatcher,
            DictionaryEvidenceEnabled = DictionaryEvidenceEnabled,
            DictionaryEvidenceWeight = Math.Clamp(DictionaryEvidenceWeight, 0.0, 1.0),
            DictionaryMinEvidenceScore = Math.Clamp(DictionaryMinEvidenceScore, -1.0, 1.0),
            DictionaryRequestTimeoutMs = Math.Clamp(DictionaryRequestTimeoutMs, 100, 5000),
            DictionaryProvider = _dictionaryDefinitionProvider
        };
    }
    [RelayCommand(CanExecute = nameof(CanClearTagOverrides))]
    private void ClearTagOverrides()
    {
        _exploreController.ClearTagOverridesForCurrentPack();
    }

    private bool CanClearTagOverrides() => HasScannedArchive;

    /// <summary>Replace custom tag rules from JSON (file picker calls this). Persists settings.</summary>
    public string? ImportCustomTagRulesFromJson(string json)
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<CustomTagRuleEntry>>(json);
            if (list is null)
            {
                return "Invalid or empty tag rules file.";
            }

            Rulesets.ReplaceCustomRules(list);

            SaveSettings();
            NotifyTagRulesChanged();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Debug report text for the MiniLM semantic match dialog. Bound to the dialog TextBlock.</summary>
    [ObservableProperty] private string _semanticMatchDebugText = "";

    /// <summary>Set by the view so the command can open a dialog.</summary>
    internal Action<string>? ShowSemanticDebugDialog { get; set; }

    [RelayCommand]
    private void DebugSemanticMatch(ArchiveNode? node)
    {
        if (node is null || node.IsFolder || !UseSemanticMaterialTags)
        {
            return;
        }

        var debugText = _exploreController.GetSemanticMatchDebugText(node.FullPath);
        if (string.IsNullOrWhiteSpace(debugText))
        {
            SemanticMatchDebugText = "MiniLM is not enabled or the file is not a texture.";
            ShowSemanticDebugDialog?.Invoke(SemanticMatchDebugText);
            return;
        }

        SemanticMatchDebugText = debugText;
        ShowSemanticDebugDialog?.Invoke(SemanticMatchDebugText);
    }
}
