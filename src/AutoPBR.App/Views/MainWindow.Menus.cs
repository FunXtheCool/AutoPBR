using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    internal void HandleBrowseInputClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        BrowseInput_Click(sender, e);

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            if (vm.UseBatchFolderInput)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = Lang.Resources.BatchFolderWatermark,
                    AllowMultiple = false
                });

                var folderPath = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                if (folderPath is not null)
                {
                    vm.BatchFolderPath = folderPath;
                }
            }
            else
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = Lang.Resources.SelectResourcePackTitle,
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Zip / JAR") { Patterns = ["*.zip", "*.jar"] }
                    ]
                });

                var filePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
                if (filePath is not null)
                {
                    vm.PackPath = filePath;
                }
            }
        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }
}
