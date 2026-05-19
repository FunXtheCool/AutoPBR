namespace AutoPBR.GeometryCompiler.Tests;

internal static class GeometryIrRepoPaths
{
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

    public static string GeometryShardPath(string repoRoot, string versionLabel, string officialJvmName) =>
        Path.Combine(GeneratedRoot(repoRoot), "geometry", versionLabel, $"{officialJvmName}.json");

    public static string NativeDataRoot(string repoRoot) =>
        Path.Combine(repoRoot, "src", "AutoPBR.Core", "Data", "minecraft-native");

    internal static string ClassPathLineToOfficialJvmName(string line)
    {
        var s = line.Trim().Replace('/', '.');
        if (s.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
        {
            s = s[..^6];
        }

        return s;
    }

    public static IReadOnlyList<string> LoadOfficialJvmNamesFromClassList(string repoRoot, string listFileName)
    {
        var listPath = Path.Combine(NativeDataRoot(repoRoot), listFileName);
        return File.ReadAllLines(listPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(ClassPathLineToOfficialJvmName)
            .ToList();
    }

    public static IReadOnlyList<string> LoadFullModelClassList(string repoRoot) =>
        LoadOfficialJvmNamesFromClassList(repoRoot, "minecraft_1.21.11_client_model_classes.txt");

    public static IReadOnlyList<string> LoadPhase8StrictOkClassList(string repoRoot) =>
        LoadOfficialJvmNamesFromClassList(repoRoot, "minecraft_1.21.11_geometry_strict_ok_model_classes.txt");

    public static IReadOnlyList<string> LoadPartialToOkPromotionJvmNames(string repoRoot) =>
        AutoPBR.Tests.Shared.GeometryIrTestTierSupport.LoadOfficialJvmNames(
            repoRoot,
            "geometry_ir_partial_to_ok_promotion_jvm.txt");

    public static string? ClientJar12111(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "tools", "minecraft-parity", VersionLabel12111, "client.jar");
        return File.Exists(path) ? path : null;
    }

    public static string Mappings12111(string repoRoot) =>
        Path.Combine(repoRoot, "tools", "minecraft-parity", VersionLabel12111, "client_mappings.txt");
}
