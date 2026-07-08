using AutoPBR.App.Controls;
using AutoPBR.App.ViewModels;

using Avalonia.Controls;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            PreviewSidebar?.WireViewModel(vm);
        }

        WireExploreJumpToTopOnLoaded();
    }
}
