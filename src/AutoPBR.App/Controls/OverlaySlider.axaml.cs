using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AutoPBR.App.Controls;

public partial class OverlaySlider : UserControl
{
  public static readonly StyledProperty<double> ValueProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(Value), defaultValue: 0d, defaultBindingMode: BindingMode.TwoWay);

  public static readonly StyledProperty<double> MinimumProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(Minimum), defaultValue: 0d);

  public static readonly StyledProperty<double> MaximumProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(Maximum), defaultValue: 100d);

  public static readonly StyledProperty<double> SmallChangeProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(SmallChange), defaultValue: 0.01d);

  public static readonly StyledProperty<double> LargeChangeProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(LargeChange), defaultValue: 0.1d);

  public static readonly StyledProperty<double> TickFrequencyProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(TickFrequency), defaultValue: 0d);

  public static readonly StyledProperty<bool> IsSnapToTickEnabledProperty =
      AvaloniaProperty.Register<OverlaySlider, bool>(nameof(IsSnapToTickEnabled));

  public static readonly StyledProperty<double> IncrementProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(Increment), defaultValue: 0.01d);

  public static readonly StyledProperty<string> FormatStringProperty =
      AvaloniaProperty.Register<OverlaySlider, string>(nameof(FormatString), defaultValue: "0.##");

  public static readonly StyledProperty<double> BoxWidthProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(BoxWidth), defaultValue: 72d);

  public static readonly StyledProperty<double> BoxMinWidthProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(BoxMinWidth), defaultValue: 72d);

  public static readonly StyledProperty<double> BoxMaxWidthProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(BoxMaxWidth), defaultValue: 120d);

  public static readonly StyledProperty<double> SliderWidthProperty =
      AvaloniaProperty.Register<OverlaySlider, double>(nameof(SliderWidth), defaultValue: 220d);

  public double Value
  {
    get => GetValue(ValueProperty);
    set => SetValue(ValueProperty, value);
  }

  public double Minimum
  {
    get => GetValue(MinimumProperty);
    set => SetValue(MinimumProperty, value);
  }

  public double Maximum
  {
    get => GetValue(MaximumProperty);
    set => SetValue(MaximumProperty, value);
  }

  public double SmallChange
  {
    get => GetValue(SmallChangeProperty);
    set => SetValue(SmallChangeProperty, value);
  }

  public double LargeChange
  {
    get => GetValue(LargeChangeProperty);
    set => SetValue(LargeChangeProperty, value);
  }

  public double TickFrequency
  {
    get => GetValue(TickFrequencyProperty);
    set => SetValue(TickFrequencyProperty, value);
  }

  public bool IsSnapToTickEnabled
  {
    get => GetValue(IsSnapToTickEnabledProperty);
    set => SetValue(IsSnapToTickEnabledProperty, value);
  }

  public double Increment
  {
    get => GetValue(IncrementProperty);
    set => SetValue(IncrementProperty, value);
  }

  public string FormatString
  {
    get => GetValue(FormatStringProperty);
    set => SetValue(FormatStringProperty, value);
  }

  public double BoxWidth
  {
    get => GetValue(BoxWidthProperty);
    set => SetValue(BoxWidthProperty, value);
  }

  public double BoxMinWidth
  {
    get => GetValue(BoxMinWidthProperty);
    set => SetValue(BoxMinWidthProperty, value);
  }

  public double BoxMaxWidth
  {
    get => GetValue(BoxMaxWidthProperty);
    set => SetValue(BoxMaxWidthProperty, value);
  }

  public double SliderWidth
  {
    get => GetValue(SliderWidthProperty);
    set => SetValue(SliderWidthProperty, value);
  }

  public OverlaySlider()
  {
    InitializeComponent();

    ValueBox.AddHandler(
        PointerReleasedEvent,
        OnValueBoxPointerReleased,
        RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
        handledEventsToo: true);

    SyncValueBoxToolTip();
  }

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
  {
    base.OnPropertyChanged(change);
    if (change.Property == ToolTip.TipProperty)
    {
      SyncValueBoxToolTip();
    }
  }

  private void SyncValueBoxToolTip() => ToolTip.SetTip(ValueBox, ToolTip.GetTip(this));

  private void OnValueBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (!IsEnabled || e.InitialPressMouseButton != MouseButton.Left)
    {
      return;
    }

    // Defer so the same click is not treated as an outside dismiss by the flyout layer.
    Dispatcher.UIThread.Post(ShowSliderFlyout, DispatcherPriority.Background);
  }

  private void ShowSliderFlyout()
  {
    if (!IsEnabled)
    {
      return;
    }

    if (FlyoutBase.GetAttachedFlyout(ValueBox) is not Flyout flyout || flyout.IsOpen)
    {
      return;
    }

    FlyoutBase.ShowAttachedFlyout(ValueBox);
    ValueSlider.Focus();
  }
}
