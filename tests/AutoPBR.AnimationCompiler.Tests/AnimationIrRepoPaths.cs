namespace AutoPBR.AnimationCompiler.Tests;

internal static class AnimationIrRepoPaths
{
    public const string VersionLabel262 = "26.1.2";
    public const string VersionLabel12111 = "1.21.11";

    public static string FindRepoRoot()
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

    public static string GeneratedRoot(string repoRoot) => Path.Combine(repoRoot, "docs", "generated");

    public static string AnimationShardPath(string repoRoot, string officialJvmName, string versionLabel = VersionLabel262) =>
        Path.Combine(GeneratedRoot(repoRoot), "animation", versionLabel, $"{officialJvmName}.json");

    public static string? ClientJar(string repoRoot, string versionLabel)
    {
        var path = Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "client.jar");
        return File.Exists(path) ? path : null;
    }

    public static string MappingsPath(string repoRoot, string versionLabel) =>
        Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "client_mappings.txt");

    public static string AnimationJavapcPath(string repoRoot, string officialJvmName)
    {
        var fileStem = officialJvmName.Replace(".", "_", StringComparison.Ordinal);
        return Path.Combine(
            GeneratedRoot(repoRoot),
            "minecraft-client-model-index-26.1.2-animation-init",
            $"{fileStem}.javapc.txt");
    }

    internal static string ClassPathLineToOfficialJvmName(string line)
    {
        var s = line.Trim().Replace('/', '.');
        if (s.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
        {
            s = s[..^6];
        }

        return s;
    }

    public static IReadOnlyList<string> LoadOfficialJvmNamesFromBatchList(string repoRoot, string listFileName) =>
        File.ReadAllLines(Path.Combine(
                repoRoot,
                "src",
                "AutoPBR.Core",
                "Data",
                "minecraft-native",
                listFileName))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(ClassPathLineToOfficialJvmName)
            .ToList();

    public static IReadOnlyList<string> LoadOfficialJvmNamesFromBatchList(string repoRoot) =>
        LoadOfficialJvmNamesFromBatchList(repoRoot, "minecraft_26.1.2_client_animation_definition_classes.txt");
}
