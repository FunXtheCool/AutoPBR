using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace AutoPBR.App.Controls;

public class FlyoutSection : TemplatedControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<FlyoutSection, object?>(nameof(Header));

    public static readonly StyledProperty<object?> HeaderToolTipProperty =
        AvaloniaProperty.Register<FlyoutSection, object?>(nameof(HeaderToolTip));

    public static readonly StyledProperty<object?> FlyoutContentProperty =
        AvaloniaProperty.Register<FlyoutSection, object?>(nameof(FlyoutContent));

    public static readonly StyledProperty<double> FlyoutMinWidthProperty =
        AvaloniaProperty.Register<FlyoutSection, double>(nameof(FlyoutMinWidth), 320d);

    public static readonly StyledProperty<double> FlyoutMaxWidthProperty =
        AvaloniaProperty.Register<FlyoutSection, double>(nameof(FlyoutMaxWidth), 480d);

    public static readonly StyledProperty<double> FlyoutMaxHeightProperty =
        AvaloniaProperty.Register<FlyoutSection, double>(nameof(FlyoutMaxHeight), 420d);

    public static readonly StyledProperty<double> ButtonMinWidthProperty =
        AvaloniaProperty.Register<FlyoutSection, double>(nameof(ButtonMinWidth), 0d);

    static FlyoutSection()
    {
        HorizontalAlignmentProperty.OverrideDefaultValue<FlyoutSection>(Avalonia.Layout.HorizontalAlignment.Stretch);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? HeaderToolTip
    {
        get => GetValue(HeaderToolTipProperty);
        set => SetValue(HeaderToolTipProperty, value);
    }

    [Content]
    public object? FlyoutContent
    {
        get => GetValue(FlyoutContentProperty);
        set => SetValue(FlyoutContentProperty, value);
    }

    public double FlyoutMinWidth
    {
        get => GetValue(FlyoutMinWidthProperty);
        set => SetValue(FlyoutMinWidthProperty, value);
    }

    public double FlyoutMaxWidth
    {
        get => GetValue(FlyoutMaxWidthProperty);
        set => SetValue(FlyoutMaxWidthProperty, value);
    }

    public double FlyoutMaxHeight
    {
        get => GetValue(FlyoutMaxHeightProperty);
        set => SetValue(FlyoutMaxHeightProperty, value);
    }

    public double ButtonMinWidth
    {
        get => GetValue(ButtonMinWidthProperty);
        set => SetValue(ButtonMinWidthProperty, value);
    }
}
