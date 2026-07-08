using System.Text.Json;
using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

internal static class RepairParityShardCommand
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    public static int Run(string[] args)
    {
        if (args.Length == 0 || HasHelp(args))
        {
            Console.Error.WriteLine("Usage: repair-parity-shard <officialJvmName> [--version-label 26.1.2]");
            return args.Length == 0 ? 1 : 0;
        }

        var jvm = args[0];
        var versionLabel = "26.1.2";
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--version-label", StringComparison.OrdinalIgnoreCase))
            {
                versionLabel = args[i + 1];
            }
        }

        var root = Program.FindRepoRoot();
        var shardPath = Path.Combine(root, "docs", "generated", "geometry", versionLabel, $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            Console.Error.WriteLine($"Shard not found: {shardPath}");
            return 2;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        File.WriteAllText(shardPath, JsonSerializer.Serialize(JsonDocument.Parse(repaired.GetRawText()).RootElement, WriteIndentedJson));
        Console.WriteLine($"Repaired parity shard: {shardPath}");
        return 0;
    }

    private static bool HasHelp(string[] args) =>
        args.Any(a => string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase));
}
