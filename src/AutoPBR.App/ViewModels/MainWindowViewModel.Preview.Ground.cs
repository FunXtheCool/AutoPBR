using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

using Avalonia.Threading;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private CancellationTokenSource? _previewGroundTextureCts;
    private PreviewTextureMaps? _previewGroundSourceMaps;

    private void SchedulePreviewGroundTextureRefresh()
    {
        if (!IsPreview3D)
        {
            return;
        }

        _ = RefreshPreviewGroundTextureAsync();
    }

    private async Task RefreshPreviewGroundTextureAsync()
    {
        _previewGroundTextureCts?.Cancel();
        _previewGroundTextureCts?.Dispose();
        var cts = new CancellationTokenSource();
        _previewGroundTextureCts = cts;

        try
        {
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            string? diskPack = null;
            if (PreviewGroundMapsResolver.ShouldPreferScannedPack(HasScannedArchive, IsBatchScanActive) &&
                _exploreController.TryGetDiskPackAndEntryPath(
                    PreviewGroundMapsResolver.GrassBlockTopArchivePath,
                    out var pack,
                    out _))
            {
                diskPack = pack;
            }

            var options = BuildConversionOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
            var maps = await PreviewGroundMapsResolver.TryResolveAsync(
                    diskPack,
                    diskPack is not null,
                    MinecraftAssetsDirectory,
                    options,
                    cts.Token)
                .ConfigureAwait(false);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested || _glPreview is null || !IsPreview3D)
                {
                    return;
                }

                _previewGroundSourceMaps = maps;
                EnsurePreviewGrassColormapLoaded();
                _glPreview.SetGroundMaterial(BuildPreviewGroundMaterial());
            });
        }
        catch (OperationCanceledException)
        {
            /* superseded */
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cts.IsCancellationRequested)
                {
                    AddLogLine($"[Preview 3D] Ground texture resolve failed: {ex.Message}");
                }
            });
        }
    }

    private PreviewMaterial? BuildPreviewGroundMaterial()
    {
        if (_previewGroundSourceMaps is not null)
        {
            return PreviewMaterialMapper.FromCoreMaps(
                ApplyGrassColormapTintIfNeeded(
                    _previewGroundSourceMaps,
                    PreviewGroundMapsResolver.GrassBlockTopArchivePath),
                PreviewGroundMapsResolver.GrassBlockTopArchivePath);
        }

        return PreviewBundledGroundMapsLoader.TryLoad(out var bundled) ? bundled : null;
    }

    private void PushPreviewGroundMaterialToGpu()
    {
        if (_glPreview is null || !IsPreview3D)
        {
            return;
        }

        _glPreview.SetGroundMaterial(BuildPreviewGroundMaterial());
    }
}
