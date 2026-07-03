using System.Globalization;
using System.Text;
using System.Text.Json;

using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new() { WriteIndented = true };
    private static readonly CompositeFormat LogTagRulesImportFailedFormat =
        CompositeFormat.Parse(Lang.Resources.Log_TagRulesImportFailed);
    private static readonly CompositeFormat LogTagRulesImportedFormat =
        CompositeFormat.Parse(Lang.Resources.Log_TagRulesImported);
    private async void ExportTagRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Resources.ExportTagRules,
                DefaultExtension = "json",
                SuggestedFileName = "auto_pbr_custom_tag_rules.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] }
                ]
            });
            var path = file?.TryGetLocalPath();
            if (path is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(vm.CustomTagRules.ToList(), IndentedJsonSerializerOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(true);
            vm.AppendUserLog(Lang.Resources.Log_TagRulesExported);
        }
        catch
        {
            // ignore
        }
    }

    private async void ImportTagRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Lang.Resources.ImportTagRules,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] }
                ]
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(true);
            var err = vm.ImportCustomTagRulesFromJson(json);
            vm.AppendUserLog(err is not null
                ? string.Format(CultureInfo.InvariantCulture, LogTagRulesImportFailedFormat, err)
                : string.Format(CultureInfo.InvariantCulture, LogTagRulesImportedFormat, vm.CustomTagRules.Count));
        }
        catch
        {
            // ignore
        }
    }

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

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private async void BrowseMinecraftAssets_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
