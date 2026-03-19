using System.Globalization;
using Avalonia.Data.Converters;

namespace AutoPBR.App.Converters;

/// <summary>Bind optional float? to TextBox; empty string → null.</summary>
public sealed class NullableFloatStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null;
    }
}
