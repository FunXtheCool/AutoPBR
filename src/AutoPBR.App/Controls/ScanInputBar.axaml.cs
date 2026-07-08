using AutoPBR.App.Views;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoPBR.App.Controls;

public partial class ScanInputBar : UserControl
{
    public ScanInputBar()
    {
        InitializeComponent();
    }

    private void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is MainWindow window)
        {
            window.HandleBrowseInputClick(sender, e);
        }
    }
}
