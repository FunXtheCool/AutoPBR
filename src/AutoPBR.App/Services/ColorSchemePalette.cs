using Avalonia.Media;

namespace AutoPBR.App.Services;

/// <summary>Brushes and colors for a single color scheme. Returned by <see cref="AppearanceService"/>.</summary>
internal sealed class ColorSchemePalette
{
    public IBrush WindowBackground { get; init; } = Brushes.Transparent;
    public IBrush CardBackground { get; init; } = Brushes.Transparent;
    public Color PreviewFadeColor { get; init; }
    public IBrush CardBorderBrush { get; init; } = Brushes.Gray;
    public IBrush AccentBrush { get; init; } = Brushes.DeepSkyBlue;
    public IBrush ForegroundBrush { get; init; } = Brushes.White;
}
