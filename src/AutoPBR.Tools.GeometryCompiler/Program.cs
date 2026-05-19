namespace AutoPBR.Tools.GeometryCompiler;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || HasHelp(args))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (string.Equals(args[0], "validate-geometry", StringComparison.OrdinalIgnoreCase))
        {
            return GeometryIrStructuralValidator.RunValidateGeometryCommand(args.Skip(1).ToArray());
        }

        if (string.Equals(args[0], "export-javap-pilot-snapshots", StringComparison.OrdinalIgnoreCase))
        {
            return ExportAssemblyPilotJavapSnapshotsCommand.Run(args.Skip(1).ToArray());
        }

        if (string.Equals(args[0], "score-lift-shard", StringComparison.OrdinalIgnoreCase))
        {
            return ScoreLiftShardCommand.Run(args.Skip(1).ToArray());
        }

        if (string.Equals(args[0], "codegen-entity-cuboids", StringComparison.OrdinalIgnoreCase))
        {
            return GeometryIrCodegenHost.RunCodegenEntityCuboids(args.Skip(1).ToArray());
        }

        if (string.Equals(args[0], "migrate-v2", StringComparison.OrdinalIgnoreCase))
        {
            var root = FindRepoRoot();
            var gen = Path.Combine(root, "docs", "generated", "geometry");
            if (args.Length > 1)
            {
                foreach (var label in args.Skip(1))
                {
                    GeometryIrV2Migration.MigrateDirectory(Path.Combine(gen, label));
                }
            }
            else
            {
                foreach (var dir in Directory.EnumerateDirectories(gen))
                {
                    GeometryIrV2Migration.MigrateDirectory(dir);
                }
            }

            Console.WriteLine("Geometry IR v2 migration complete.");
            return 0;
        }

        var opt = ArgMap.Parse(args);
        if (opt.PrintAnimationSummary is { } animPath)
        {
            var root = FindRepoRoot();
            var gen = Path.Combine(root, "docs", "generated");
            if (!AnimationClinitPrototype.TryReadSummaryLine(gen, animPath, out var sum))
            {
                Console.Error.WriteLine($"Animation sidecar not found under docs/generated: {animPath}");
                return 4;
            }

            Console.WriteLine(sum);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(opt.ClientJar))
        {
            Console.Error.WriteLine("--client-jar is required unless using --print-animation-summary.");
            return 2;
        }

        var outDir = string.IsNullOrWhiteSpace(opt.OutDir)
            ? Path.Combine(FindRepoRoot(), "docs", "generated")
            : Path.GetFullPath(opt.OutDir);

        var parallelism = ResolveMaxParallelism(opt);
        // ProGuard jars need bytecode ASM disassembly (obfuscated method names); named jars use ASM by default too.
        var useAsmLift = opt.UseAsmLift || string.IsNullOrWhiteSpace(opt.Mappings) ||
                         !string.IsNullOrWhiteSpace(opt.Mappings);
        var host = new GeometryCompilerHost(opt.ClientJar!, opt.Mappings, opt.VersionLabel ?? "26.1.2", outDir, opt.Javap,
            parallelism, opt.Quiet, opt.Stats, useAsmLift, opt.CompareLift);
        var method = string.IsNullOrWhiteSpace(opt.FactoryMethod) ? "createBodyLayer" : opt.FactoryMethod!;

        if (!string.IsNullOrWhiteSpace(opt.Single))
        {
            return host.RunSingle(opt.Single!, method);
        }

        if (!string.IsNullOrWhiteSpace(opt.BatchList))
        {
            return host.RunBatch(Path.GetFullPath(opt.BatchList!), method);
        }

        PrintUsage();
        return 1;
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
            AutoPBR geometry compiler

            Usage:
              AutoPBR.Tools.GeometryCompiler validate-geometry [--strict] [--adjacency] [--require-ok] [--out-dir <docs/generated>] [versionLabel]

              AutoPBR.Tools.GeometryCompiler --client-jar <client.jar> [--mappings <client_mappings.txt>]
                [--use-asm-lift] [--compare-lift]
                [--version-label 26.1.2] [--out-dir <docs/generated>] [--javap <path>]
                (--single <official.jvm.ClassName> | --batch-list <model_classes.txt>)
                [--factory-method createBodyLayer]
                [--parallel] [--max-parallelism <n>] [--quiet] [--stats]

            Batch mode processes every class in the list: writes geometry/<versionLabel>/<fqn>.json (synthesizing
            a minimal shard when none exists) and rewrites geometry-index-<versionLabel>.json.
              --parallel             Batch only: run up to min(8, processor count) classes in parallel.
              --max-parallelism <n>  Batch only: cap parallel class workers (implies parallel if n > 1).
              --quiet                Batch only: suppress per-shard "Wrote ..." lines (errors still print).
              --stats                After batch: print javap/cache counters to stderr.

            Examples:
              dotnet run --project src/AutoPBR.Tools.GeometryCompiler -- --client-jar tools/minecraft-parity/26.1.2/client.jar --single net.minecraft.client.model.animal.cow.CowModel

              dotnet run --project src/AutoPBR.Tools.GeometryCompiler -- --print-animation-summary minecraft-client-model-index-26.1.2-animation-init/net_minecraft_client_animation_definitions_ArmadilloAnimation.javapc.txt
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
        public string? ClientJar { get; init; }
        public string? Mappings { get; init; }
        public string? VersionLabel { get; init; }
        public string? OutDir { get; init; }
        public string? Single { get; init; }
        public string? BatchList { get; init; }
        public string? FactoryMethod { get; init; }
        public string? Javap { get; init; }
        public string? PrintAnimationSummary { get; init; }
        public int? MaxParallelism { get; init; }
        public bool ParallelInvoked { get; init; }
        public bool Quiet { get; init; }
        public bool Stats { get; init; }
        public bool UseAsmLift { get; init; }
        public bool CompareLift { get; init; }

        public static ArgMap Parse(string[] args)
        {
            string? client = null, maps = null, ver = null, odir = null, single = null, batch = null, fact = null,
                javap = null, anim = null;
            int? maxPar = null;
            var parallelFlag = false;
            var quiet = false;
            var stats = false;
            var useAsmLift = false;
            var compareLift = false;
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                string? Next() => i + 1 < args.Length ? args[++i] : null;
                if (string.Equals(a, "--client-jar", StringComparison.OrdinalIgnoreCase))
                {
                    client = Next();
                }
                else if (string.Equals(a, "--mappings", StringComparison.OrdinalIgnoreCase))
                {
                    maps = Next();
                }
                else if (string.Equals(a, "--version-label", StringComparison.OrdinalIgnoreCase))
                {
                    ver = Next();
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
                else if (string.Equals(a, "--factory-method", StringComparison.OrdinalIgnoreCase))
                {
                    fact = Next();
                }
                else if (string.Equals(a, "--javap", StringComparison.OrdinalIgnoreCase))
                {
                    javap = Next();
                }
                else if (string.Equals(a, "--print-animation-summary", StringComparison.OrdinalIgnoreCase))
                {
                    anim = Next();
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
                else if (string.Equals(a, "--use-asm-lift", StringComparison.OrdinalIgnoreCase))
                {
                    useAsmLift = true;
                }
                else if (string.Equals(a, "--compare-lift", StringComparison.OrdinalIgnoreCase))
                {
                    compareLift = true;
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
                FactoryMethod = fact,
                Javap = javap,
                PrintAnimationSummary = anim,
                MaxParallelism = maxPar,
                ParallelInvoked = parallelFlag,
                Quiet = quiet,
                Stats = stats,
                UseAsmLift = useAsmLift,
                CompareLift = compareLift
            };
        }
    }
}
