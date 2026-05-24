using AutoPBR.Core.Models;

using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.ViewModels;

public partial class UvDebugWindowViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public UvDebugWindowViewModel(MainWindowViewModel main)
    {
        _main = main;
        _main.PropertyChanged += MainOnPropertyChanged;

        _flipU = UvDebugSettings.FlipU;
        _flipV = UvDebugSettings.FlipV;
        _offsetUPixels = UvDebugSettings.OffsetUPixels;
        _offsetVPixels = UvDebugSettings.OffsetVPixels;
        _globalFaceRotationDegrees = UvDebugSettings.GlobalFaceRotationDegrees;
        _swapFaceNorthSouth = UvDebugSettings.SwapFaceNorthSouth;
        _swapFaceEastWest = UvDebugSettings.SwapFaceEastWest;
        _swapFaceUpDown = UvDebugSettings.SwapFaceUpDown;
        _preserveDirectionalBounds = UvDebugSettings.PreserveDirectionalBounds;
        _useBottomLeftUvOrigin = UvDebugSettings.UseBottomLeftUvOrigin;
        _uvCornerOrderMode = UvDebugSettings.UvCornerOrderMode;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Avalonia binding ({Binding HeaderTitle}) requires an instance member.")]
    public string HeaderTitle => "UV Debug";

    public IBrush WindowBackground => _main.WindowBackground;
    public IBrush CardBackground => _main.CardBackground;
    public IBrush CardBorderBrush => _main.CardBorderBrush;
    public IBrush ForegroundBrush => _main.ForegroundBrush;
    public IBrush AccentBrush => _main.AccentBrush;

    [ObservableProperty] private bool _flipU;
    [ObservableProperty] private bool _flipV;
    [ObservableProperty] private double _offsetUPixels;
    [ObservableProperty] private double _offsetVPixels;
    [ObservableProperty] private int _globalFaceRotationDegrees;
    [ObservableProperty] private bool _swapFaceNorthSouth;
    [ObservableProperty] private bool _swapFaceEastWest;
    [ObservableProperty] private bool _swapFaceUpDown;
    [ObservableProperty] private bool _preserveDirectionalBounds;
    [ObservableProperty] private bool _useBottomLeftUvOrigin;
    [ObservableProperty] private int _uvCornerOrderMode;

    public bool Rotation0
    {
        get => GlobalFaceRotationDegrees == 0;
        set
        {
            if (value)
            {
                GlobalFaceRotationDegrees = 0;
            }
        }
    }

    public bool Rotation90
    {
        get => GlobalFaceRotationDegrees == 90;
        set
        {
            if (value)
            {
                GlobalFaceRotationDegrees = 90;
            }
        }
    }

    public bool Rotation180
    {
        get => GlobalFaceRotationDegrees == 180;
        set
        {
            if (value)
            {
                GlobalFaceRotationDegrees = 180;
            }
        }
    }

    public bool Rotation270
    {
        get => GlobalFaceRotationDegrees == 270;
        set
        {
            if (value)
            {
                GlobalFaceRotationDegrees = 270;
            }
        }
    }

    public bool CornerOrderDefault
    {
        get => UvCornerOrderMode == 0;
        set
        {
            if (value)
            {
                UvCornerOrderMode = 0;
            }
        }
    }

    public bool CornerOrderRotate90
    {
        get => UvCornerOrderMode == 1;
        set
        {
            if (value)
            {
                UvCornerOrderMode = 1;
            }
        }
    }

    public bool CornerOrderRotate180
    {
        get => UvCornerOrderMode == 2;
        set
        {
            if (value)
            {
                UvCornerOrderMode = 2;
            }
        }
    }

    public bool CornerOrderRotate270
    {
        get => UvCornerOrderMode == 3;
        set
        {
            if (value)
            {
                UvCornerOrderMode = 3;
            }
        }
    }

    public bool CornerOrderReverseWinding
    {
        get => UvCornerOrderMode == 4;
        set
        {
            if (value)
            {
                UvCornerOrderMode = 4;
            }
        }
    }

    public void Detach() => _main.PropertyChanged -= MainOnPropertyChanged;

    partial void OnFlipUChanged(bool value)
    {
        UvDebugSettings.FlipU = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnFlipVChanged(bool value)
    {
        UvDebugSettings.FlipV = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnOffsetUPixelsChanged(double value)
    {
        UvDebugSettings.OffsetUPixels = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnOffsetVPixelsChanged(double value)
    {
        UvDebugSettings.OffsetVPixels = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnGlobalFaceRotationDegreesChanged(int value)
    {
        var snapped = NormalizeRotation(value);
        if (snapped != value)
        {
            GlobalFaceRotationDegrees = snapped;
            return;
        }

        UvDebugSettings.GlobalFaceRotationDegrees = snapped;
        OnPropertyChanged(nameof(Rotation0));
        OnPropertyChanged(nameof(Rotation90));
        OnPropertyChanged(nameof(Rotation180));
        OnPropertyChanged(nameof(Rotation270));
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnSwapFaceNorthSouthChanged(bool value)
    {
        UvDebugSettings.SwapFaceNorthSouth = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnSwapFaceEastWestChanged(bool value)
    {
        UvDebugSettings.SwapFaceEastWest = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnSwapFaceUpDownChanged(bool value)
    {
        UvDebugSettings.SwapFaceUpDown = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnPreserveDirectionalBoundsChanged(bool value)
    {
        UvDebugSettings.PreserveDirectionalBounds = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnUseBottomLeftUvOriginChanged(bool value)
    {
        UvDebugSettings.UseBottomLeftUvOrigin = value;
        _main.TriggerPreviewRefreshForDebug();
    }

    partial void OnUvCornerOrderModeChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 4);
        if (clamped != value)
        {
            UvCornerOrderMode = clamped;
            return;
        }

        UvDebugSettings.UvCornerOrderMode = clamped;
        OnPropertyChanged(nameof(CornerOrderDefault));
        OnPropertyChanged(nameof(CornerOrderRotate90));
        OnPropertyChanged(nameof(CornerOrderRotate180));
        OnPropertyChanged(nameof(CornerOrderRotate270));
        OnPropertyChanged(nameof(CornerOrderReverseWinding));
        _main.TriggerPreviewRefreshForDebug();
    }

    private void MainOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.WindowBackground) or
            nameof(MainWindowViewModel.CardBackground) or
            nameof(MainWindowViewModel.CardBorderBrush) or
            nameof(MainWindowViewModel.ForegroundBrush) or
            nameof(MainWindowViewModel.AccentBrush))
        {
            OnPropertyChanged(nameof(WindowBackground));
            OnPropertyChanged(nameof(CardBackground));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(AccentBrush));
        }
    }

    private static int NormalizeRotation(int value)
    {
        var normalized = ((value % 360) + 360) % 360;
        return normalized switch
        {
            < 45 => 0,
            < 135 => 90,
            < 225 => 180,
            < 315 => 270,
            _ => 0,
        };
    }
}

