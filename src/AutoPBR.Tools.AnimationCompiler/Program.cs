namespace AutoPBR.Tools.AnimationCompiler;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || HasHelp(args))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var opt = ArgMap.Parse(args);
        if (string.IsNullOrWhiteSpace(opt.ClientJar))
        {
            Console.Error.WriteLine("--client-jar is required.");
            return 2;
        }

        var outDir = string.IsNullOrWhiteSpace(opt.OutDir)
            ? Path.Combine(FindRepoRoot(), "docs", "generated")
            : Path.GetFullPath(opt.OutDir!);

        var parallelism = ResolveMaxParallelism(opt);
        var versionLabel = opt.VersionLabel ?? "26.1.2";

        if (opt.LiftRendererState)
        {
            var rendererStateHost = new RendererStateCompilerHost(
                opt.ClientJar!,
                versionLabel,
                outDir,
                opt.Javap,
                parallelism,
                opt.Quiet);
            if (!string.IsNullOrWhiteSpace(opt.Single))
            {
                return rendererStateHost.RunSingle(opt.Single!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(opt.BatchList))
            {
                return rendererStateHost.RunBatch(Path.GetFullPath(opt.BatchList!));
            }

            Console.Error.WriteLine("--lift-renderer-state requires --single <renderer.jvm.Class> or --batch-list <renderer-classes.txt>.");
            return 2;
        }

        if (opt.LiftSetupAnim)
        {
            var setupHost = new SetupAnimCompilerHost(opt.ClientJar!, versionLabel, outDir, opt.Javap,
                parallelism, opt.Quiet, opt.Stats);
            if (!string.IsNullOrWhiteSpace(opt.Single))
            {
                return setupHost.RunSingle(opt.Single!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(opt.BatchList))
            {
                return setupHost.RunBatch(Path.GetFullPath(opt.BatchList!));
            }

            var repoSetup = FindRepoRoot();
            var modelList = Path.Combine(repoSetup, "src", "AutoPBR.Core", "Data", "minecraft-native",
                $"minecraft_{versionLabel}_client_model_classes.txt");
            if (!File.Exists(modelList))
            {
                Console.Error.WriteLine($"Model class list not found: {modelList}.");
                return 2;
            }

            return setupHost.RunBatch(modelList);
        }

        var host = new AnimationCompilerHost(opt.ClientJar!, opt.Mappings, versionLabel, outDir, opt.Javap,
            parallelism, opt.Quiet, opt.Stats);

        if (!string.IsNullOrWhiteSpace(opt.Single))
        {
            return host.RunSingle(opt.Single!.Trim());
        }

        if (!string.IsNullOrWhiteSpace(opt.BatchList))
        {
            return host.RunBatch(Path.GetFullPath(opt.BatchList!));
        }

        var repo = FindRepoRoot();
        var defaultList = Path.Combine(repo, "src", "AutoPBR.Core", "Data", "minecraft-native",
            $"minecraft_{versionLabel}_client_animation_definition_classes.txt");
        if (!File.Exists(defaultList))
        {
            Console.Error.WriteLine(
                $"Default animation class list not found: {defaultList}. Pass --batch-list <path> or generate minecraft_*_client_animation_definition_classes.txt.");
            return 2;
        }

        return host.RunBatch(defaultList);
    }

    private static int ResolveMaxParallelism(ArgMap opt)
    {
        if (opt.MaxParallelism is { } n)
        {
            return n;
        }

        if (opt.ParallelInvoked)
        {
            return Math.Min(8, Math.Max(1, Environment.ProcessorCount));
        }

        return 1;
    }

    private static bool HasHelp(string[] args) =>
        args.Any(a => string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase));

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            AutoPBR animation compiler

            Usage:
              AutoPBR.Tools.AnimationCompiler --client-jar <client.jar>
                [--version-label 26.1.2] [--mappings <client_mappings.txt>] [--out-dir <docs/generated>] [--javap <path>]
                (--single <official.jvm.Class> | --batch-list <classes.txt>)
                [--lift-setup-anim] [--lift-renderer-state]
                [--parallel] [--max-parallelism <n>] [--quiet] [--stats]

            Default batch list (when neither --single nor --batch-list is set):
              AnimationDefinition: minecraft_<versionLabel>_client_animation_definition_classes.txt
              With --lift-setup-anim: minecraft_<versionLabel>_client_model_classes.txt
              With --lift-renderer-state: pass --single or --batch-list with renderer classes

            Examples:
              dotnet run --project src/AutoPBR.Tools.AnimationCompiler -- --client-jar tools/minecraft-parity/26.1.2/client.jar --version-label 26.1.2

              dotnet run --project src/AutoPBR.Tools.AnimationCompiler -- --client-jar tools/minecraft-parity/26.1.2/client.jar --single net.minecraft.client.animation.definitions.ArmadilloAnimation
            """);
    }

    internal static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "AutoPBR.sln")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed class ArgMap
    {
        public string? ClientJar { get; private init; }
        public string? Mappings { get; private init; }
        public string? VersionLabel { get; private init; }
        public string? OutDir { get; private init; }
        public string? Single { get; private init; }
        public string? BatchList { get; private init; }
        public string? Javap { get; private init; }
        public int? MaxParallelism { get; private init; }
        public bool ParallelInvoked { get; private init; }
        public bool Quiet { get; private init; }
        public bool Stats { get; private init; }
        public bool LiftSetupAnim { get; private init; }
        public bool LiftRendererState { get; private init; }

        public static ArgMap Parse(string[] args)
        {
            string? client = null, maps = null, ver = null, odir = null, single = null, batch = null, javap = null;
            int? maxPar = null;
            var parallelFlag = false;
            var quiet = false;
            var stats = false;
            var liftSetupAnim = false;
            var liftRendererState = false;
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                string? Next() => i + 1 < args.Length ? args[++i] : null;
                if (string.Equals(a, "--client-jar", StringComparison.OrdinalIgnoreCase))
                {
                    client = Next();
                }
                else if (string.Equals(a, "--version-label", StringComparison.OrdinalIgnoreCase))
                {
                    ver = Next();
                }
                else if (string.Equals(a, "--mappings", StringComparison.OrdinalIgnoreCase))
                {
                    maps = Next();
                }
                else if (string.Equals(a, "--out-dir", StringComparison.OrdinalIgnoreCase))
                {
                    odir = Next();
                }
                else if (string.Equals(a, "--single", StringComparison.OrdinalIgnoreCase))
                {
                    single = Next();
                }
                else if (string.Equals(a, "--batch-list", StringComparison.OrdinalIgnoreCase))
                {
                    batch = Next();
                }
                else if (string.Equals(a, "--javap", StringComparison.OrdinalIgnoreCase))
                {
                    javap = Next();
                }
                else if (string.Equals(a, "--parallel", StringComparison.OrdinalIgnoreCase))
                {
                    parallelFlag = true;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var pn) && pn >= 1)
                    {
                        maxPar = pn;
                        i++;
                    }
                }
                else if (string.Equals(a, "--max-parallelism", StringComparison.OrdinalIgnoreCase))
                {
                    var s = Next();
                    if (s is not null && int.TryParse(s, out var mp) && mp >= 1)
                    {
                        maxPar = mp;
                    }
                }
                else if (string.Equals(a, "--quiet", StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                }
                else if (string.Equals(a, "--stats", StringComparison.OrdinalIgnoreCase))
                {
                    stats = true;
                }
                else if (string.Equals(a, "--lift-setup-anim", StringComparison.OrdinalIgnoreCase))
                {
                    liftSetupAnim = true;
                }
                else if (string.Equals(a, "--lift-renderer-state", StringComparison.OrdinalIgnoreCase))
                {
                    liftRendererState = true;
                }
            }

            return new ArgMap
            {
                ClientJar = client,
                Mappings = maps,
                VersionLabel = ver,
                OutDir = odir,
                Single = single,
                BatchList = batch,
                Javap = javap,
                MaxParallelism = maxPar,
                ParallelInvoked = parallelFlag,
                Quiet = quiet,
                Stats = stats,
                LiftSetupAnim = liftSetupAnim,
                LiftRendererState = liftRendererState
            };
        }
    }
}
