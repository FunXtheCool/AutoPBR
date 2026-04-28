using System.Runtime.Loader;
using Avalonia;
using Avalonia.Win32;

namespace AutoPBR.App;

sealed class Program
{
    // Satellite assemblies (language resources) are moved to lang\[culture]\ in build output.
    private const string LangSubfolder = "lang";

    [STAThread]
    public static void Main(string[] args)
    {
        RegisterSatelliteAssemblyResolver();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Load satellite assemblies from lang\[culture]\ so language folders stay grouped in build output.</summary>
    private static void RegisterSatelliteAssemblyResolver()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            if (name.Name is null || !name.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var culture = name.CultureName;
            if (string.IsNullOrEmpty(culture))
            {
                return null;
            }

            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, LangSubfolder, culture, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Avoid intermittent WinUI compositor commit timeouts on some Windows setups.
            // RedirectionSurface takes a more stable composition path than direct WinUI composition.
            .With(new Win32PlatformOptions
            {
                CompositionMode = new[] { Win32CompositionMode.RedirectionSurface },
            })
            .WithInterFont()
            .LogToTrace();
}
