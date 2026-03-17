using Avalonia.Media;

namespace AutoPBR.App.Services;

/// <summary>Provides brush and color palettes for built-in color schemes.</summary>
internal static class AppearanceService
{
    public static ColorSchemePalette GetPalette(string colorScheme)
    {
        return colorScheme switch
        {
            "Dark" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x18)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2A)),
                PreviewFadeColor = Color.FromRgb(0x22, 0x22, 0x2A),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                ForegroundBrush = Brushes.White
            },
            "Blue" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x0B, 0x1B, 0x30)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x13, 0x27, 0x43)),
                PreviewFadeColor = Color.FromRgb(0x13, 0x27, 0x43),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x5B, 0x8C)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                ForegroundBrush = Brushes.White
            },
            "Green" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x0D, 0x1F, 0x16)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x14, 0x30, 0x22)),
                PreviewFadeColor = Color.FromRgb(0x14, 0x30, 0x22),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)),
                ForegroundBrush = Brushes.White
            },
            "Purple" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x22, 0x18, 0x3A)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x2E, 0x1F, 0x4D)),
                PreviewFadeColor = Color.FromRgb(0x2E, 0x1F, 0x4D),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x95, 0x7D, 0xD1)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0x86, 0xFC)),
                ForegroundBrush = Brushes.White
            },
            "Amber" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x26, 0x15, 0x06)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x3A, 0x23, 0x0B)),
                PreviewFadeColor = Color.FromRgb(0x3A, 0x23, 0x0B),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x4D)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00)),
                ForegroundBrush = Brushes.White
            },
            "Teal" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x24, 0x27)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x37, 0x3B)),
                PreviewFadeColor = Color.FromRgb(0x00, 0x37, 0x3B),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0xAB, 0xA8)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x96, 0x88)),
                ForegroundBrush = Brushes.White
            },
            "Rose" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x2B, 0x0B, 0x18)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x3B, 0x12, 0x22)),
                PreviewFadeColor = Color.FromRgb(0x3B, 0x12, 0x22),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x81, 0x82)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63)),
                ForegroundBrush = Brushes.White
            },
            "Mono" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                PreviewFadeColor = Color.FromRgb(0x2A, 0x2A, 0x2A),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                ForegroundBrush = Brushes.White
            },
            "Ocean" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x05, 0x21, 0x2F)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x0A, 0x33, 0x45)),
                PreviewFadeColor = Color.FromRgb(0x0A, 0x33, 0x45),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xD4)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x02, 0x88, 0xD1)),
                ForegroundBrush = Brushes.White
            },
            "Sunset" => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x29, 0x19, 0x14)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x3C, 0x22, 0x1B)),
                PreviewFadeColor = Color.FromRgb(0x3C, 0x22, 0x1B),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x65)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22)),
                ForegroundBrush = Brushes.White
            },
            _ => new ColorSchemePalette
            {
                WindowBackground = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x18)),
                CardBackground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2A)),
                PreviewFadeColor = Color.FromRgb(0x22, 0x22, 0x2A),
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66)),
                AccentBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                ForegroundBrush = Brushes.White
            }
        };
    }
}
