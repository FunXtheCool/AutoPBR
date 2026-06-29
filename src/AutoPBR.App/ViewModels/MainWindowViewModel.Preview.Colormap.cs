using System.IO;

using Avalonia.Media.Imaging;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using CommunityToolkit.Mvvm.ComponentModel;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private PreviewColormapImage? _previewGrassColormap;

    [ObservableProperty] private double _preview3DGrassColormapTemperature = 0.72;
    [ObservableProperty] private double _preview3DGrassColormapDownfall = 0.45;
    [ObservableProperty] private Bitmap? _preview3DGrassColormapImage;
    [ObservableProperty] private string? _preview3DGrassColormapSampleText;

    public bool IsPreview3DGrassColormapVisible =>
        IsPreview3D &&
        _previewGrassColormap is not null &&
        PreviewNeedsGrassColormapTint();

    private bool PreviewNeedsGrassColormapTint()
    {
        if (_lastPreviewModelSubject?.MaterialArchivePaths is { } paths &&
            PreviewGrassColormapTint.NeedsGrassColormapTint(paths))
        {
            return true;
        }

        return PreviewGrassColormapTint.NeedsGrassColormapTint(PreviewArchivePath);
    }

    private void RefreshPreviewGrassColormapState()
    {
        _exploreController.TryGetDiskPackAndEntryPath(PreviewArchivePath ?? "", out var diskPack, out _);
        if (!PreviewColormapLoader.TryLoadGrassColormap(diskPack, MinecraftAssetsDirectory, null, out var image) ||
            image is null)
        {
            _previewGrassColormap = null;
            Preview3DGrassColormapImage = null;
            Preview3DGrassColormapSampleText = null;
            OnPropertyChanged(nameof(IsPreview3DGrassColormapVisible));
            return;
        }

        _previewGrassColormap = image;
        Preview3DGrassColormapImage = TryBuildColormapBitmap(image);
        UpdateGrassColormapSampleText();
        OnPropertyChanged(nameof(IsPreview3DGrassColormapVisible));
    }

    private void UpdateGrassColormapSampleText()
    {
        if (_previewGrassColormap is null)
        {
            Preview3DGrassColormapSampleText = null;
            return;
        }

        var tint = SamplePreviewGrassTint();
        Preview3DGrassColormapSampleText =
            $"Grass tint RGB {tint.R},{tint.G},{tint.B} · temp {Preview3DGrassColormapTemperature:F2} · rain {Preview3DGrassColormapDownfall:F2}";
    }

    private Rgba32 SamplePreviewGrassTint() =>
        _previewGrassColormap is null
            ? new Rgba32(91, 139, 54, 255)
            : PreviewGrassColormapTint.SampleGrassTint(
                _previewGrassColormap,
                Preview3DGrassColormapTemperature,
                Preview3DGrassColormapDownfall);

    private PreviewTextureMaps ApplyGrassColormapTintIfNeeded(PreviewTextureMaps maps, string? archivePath)
    {
        if (_previewGrassColormap is null)
        {
            return maps;
        }

        return PreviewGrassColormapTint.WithGrassTint(maps, archivePath, SamplePreviewGrassTint());
    }

    private static Bitmap? TryBuildColormapBitmap(PreviewColormapImage image)
    {
        try
        {
            using var img = Image.LoadPixelData<Rgba32>(image.Rgba, image.Width, image.Height);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    partial void OnPreview3DGrassColormapTemperatureChanged(double value)
    {
        _ = value;
        UpdateGrassColormapSampleText();
        OnPropertyChanged(nameof(IsPreview3DGrassColormapVisible));
        if (IsPreview3D)
        {
            Push3DPreviewStateToGpu();
        }
    }

    partial void OnPreview3DGrassColormapDownfallChanged(double value)
    {
        _ = value;
        UpdateGrassColormapSampleText();
        OnPropertyChanged(nameof(IsPreview3DGrassColormapVisible));
        if (IsPreview3D)
        {
            Push3DPreviewStateToGpu();
        }
    }
}
