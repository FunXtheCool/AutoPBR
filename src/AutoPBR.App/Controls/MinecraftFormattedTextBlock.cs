using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;

namespace AutoPBR.App.Controls;

/// <summary>
/// TextBlock that understands Minecraft-style section formatting codes (e.g. §a, §l, §k).
/// Supports color, bold/italic/underline/strikethrough, reset, and animated obfuscated text.
/// </summary>
public sealed class MinecraftFormattedTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> FormattedTextProperty =
        AvaloniaProperty.Register<MinecraftFormattedTextBlock, string?>(nameof(FormattedText));

    private readonly List<Segment> _segments = [];
    private readonly Random _random = new();
    private DispatcherTimer? _obfuscationTimer;
    private bool _hasObfuscatedSegments;

    public string? FormattedText
    {
        get => GetValue(FormattedTextProperty);
        set => SetValue(FormattedTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FormattedTextProperty)
        {
            RebuildSegments();
        }
    }

    private void RebuildSegments()
    {
        // Avoid TextBlock's built-in text rendering path; we render via inlines only.
        Text = string.Empty;
        _segments.Clear();
        ParseSegments(FormattedText ?? string.Empty, _segments);
        _hasObfuscatedSegments = _segments.Any(s => s.Obfuscated);
        Render(randomizeObfuscated: false);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (_hasObfuscatedSegments)
        {
            _obfuscationTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(85), DispatcherPriority.Background, (_, _) => Render(randomizeObfuscated: true));
            if (!_obfuscationTimer.IsEnabled)
            {
                _obfuscationTimer.Start();
            }
        }
        else if (_obfuscationTimer is { IsEnabled: true })
        {
            _obfuscationTimer.Stop();
        }
    }

    private void Render(bool randomizeObfuscated)
    {
        Inlines?.Clear();
        foreach (var s in _segments)
        {
            var text = s.Obfuscated && randomizeObfuscated ? Obfuscate(s.Text) : s.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var run = new Run(text);
            if (s.Foreground is not null)
            {
                run.Foreground = s.Foreground;
            }

            if (s.Bold)
            {
                run.FontWeight = FontWeight.Bold;
            }

            if (s.Italic)
            {
                run.FontStyle = FontStyle.Italic;
            }

            if (s.Underline || s.Strikethrough)
            {
                var decorations = new TextDecorationCollection();
                if (s.Underline)
                {
                    decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
                }

                if (s.Strikethrough)
                {
                    decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
                }

                run.TextDecorations = decorations;
            }

            Inlines?.Add(run);
        }
    }

    private string Obfuscate(string source)
    {
        var sb = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            if (char.IsWhiteSpace(ch))
            {
                sb.Append(ch);
                continue;
            }

            sb.Append(ObfuscationChars[_random.Next(ObfuscationChars.Length)]);
        }

        return sb.ToString();
    }

    private static void ParseSegments(string input, List<Segment> output)
    {
        IBrush? foreground = null;
        var bold = false;
        var italic = false;
        var underline = false;
        var strikethrough = false;
        var obfuscated = false;
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            output.Add(new Segment(buffer.ToString(), foreground, bold, italic, underline, strikethrough, obfuscated));
            buffer.Clear();
        }

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '§' && i + 1 < input.Length)
            {
                var code = char.ToLowerInvariant(input[i + 1]);
                if (TryGetColor(code, out var color))
                {
                    Flush();
                    foreground = new SolidColorBrush(color);
                    // Minecraft behavior: color also resets formatting.
                    bold = false;
                    italic = false;
                    underline = false;
                    strikethrough = false;
                    obfuscated = false;
                    i++;
                    continue;
                }

                switch (code)
                {
                    case 'k':
                        Flush();
                        obfuscated = true;
                        i++;
                        continue;
                    case 'l':
                        Flush();
                        bold = true;
                        i++;
                        continue;
                    case 'm':
                        Flush();
                        strikethrough = true;
                        i++;
                        continue;
                    case 'n':
                        Flush();
                        underline = true;
                        i++;
                        continue;
                    case 'o':
                        Flush();
                        italic = true;
                        i++;
                        continue;
                    case 'r':
                        Flush();
                        foreground = null;
                        bold = false;
                        italic = false;
                        underline = false;
                        strikethrough = false;
                        obfuscated = false;
                        i++;
                        continue;
                }
            }

            buffer.Append(ch);
        }

        Flush();
    }

    private static bool TryGetColor(char code, out Color color)
    {
        switch (code)
        {
            case '0': color = Color.Parse("#000000"); return true;
            case '1': color = Color.Parse("#0000AA"); return true;
            case '2': color = Color.Parse("#00AA00"); return true;
            case '3': color = Color.Parse("#00AAAA"); return true;
            case '4': color = Color.Parse("#AA0000"); return true;
            case '5': color = Color.Parse("#AA00AA"); return true;
            case '6': color = Color.Parse("#FFAA00"); return true;
            case '7': color = Color.Parse("#AAAAAA"); return true;
            case '8': color = Color.Parse("#555555"); return true;
            case '9': color = Color.Parse("#5555FF"); return true;
            case 'a': color = Color.Parse("#55FF55"); return true;
            case 'b': color = Color.Parse("#55FFFF"); return true;
            case 'c': color = Color.Parse("#FF5555"); return true;
            case 'd': color = Color.Parse("#FF55FF"); return true;
            case 'e': color = Color.Parse("#FFFF55"); return true;
            case 'f': color = Color.Parse("#FFFFFF"); return true;
            // Bedrock extra material colors.
            case 'g': color = Color.Parse("#DDD605"); return true;
            case 'h': color = Color.Parse("#E3D4D1"); return true;
            case 'i': color = Color.Parse("#CECACA"); return true;
            case 'j': color = Color.Parse("#443A3B"); return true;
            case 'p': color = Color.Parse("#DEB12D"); return true;
            case 'q': color = Color.Parse("#119F36"); return true;
            case 's': color = Color.Parse("#2CBAA8"); return true;
            case 't': color = Color.Parse("#21497B"); return true;
            case 'u': color = Color.Parse("#9A5CC6"); return true;
            case 'v': color = Color.Parse("#EB7114"); return true;
            default:
                color = default;
                return false;
        }
    }

    private static readonly char[] ObfuscationChars =
    [
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm', 'n', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        '2', '3', '4', '5', '6', '7', '8', '9', '@', '#', '$', '%', '&', '*', '?'
    ];

    private sealed record Segment(
        string Text,
        IBrush? Foreground,
        bool Bold,
        bool Italic,
        bool Underline,
        bool Strikethrough,
        bool Obfuscated);
}
