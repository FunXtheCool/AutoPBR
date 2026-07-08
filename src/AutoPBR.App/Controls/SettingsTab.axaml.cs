using AutoPBR.App.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AutoPBR.App.Controls;

public partial class SettingsTab : UserControl
{
    private async void BrowseOutput_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Lang.Resources.SelectOutputFolderTitle,
                AllowMultiple = false
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.OutputDirectory = path;
            }
        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }

    private async void BrowseMinecraftAssets_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Lang.Resources.SelectMinecraftAssetsTitle,
                AllowMultiple = false
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.MinecraftAssetsDirectory = path;
            }
        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }
}
