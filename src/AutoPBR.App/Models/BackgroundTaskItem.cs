using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.Models;

/// <summary>One row in the tab-strip background task progress stack.</summary>
public partial class BackgroundTaskItem(string id, string label) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty] private string _label = label;

    /// <summary>When true, the thin progress bar runs in indeterminate mode.</summary>
    [ObservableProperty] private bool _isIndeterminate = true;

    /// <summary>0..1 when <see cref="IsIndeterminate"/> is false.</summary>
    [ObservableProperty] private double _progress;
}
