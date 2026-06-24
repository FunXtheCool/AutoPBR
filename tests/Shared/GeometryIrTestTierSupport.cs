using System.Text.Json;

namespace AutoPBR.Tests.TestSupport;

/// <summary>
/// Shared helpers for geometry IR test tiers — see docs/test-guidance-geometry-animation-ir.md.
/// </summary>
public static class GeometryIrTestTierSupport
{
    public const string DiagnosticCategory = "Diagnostic";

    /// <summary>Version label for mob-family T1 pilots (geometry_ir_mob_family_pilot_jvm.txt).</summary>
    public const string MobFamilyPilotVersionLabel = "26.1.2";

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

    public static string NativeDataRoot(string repoRoot) =>
        Path.Combine(repoRoot, "src", "AutoPBR.Core", "Data", "minecraft-native");

    public static IReadOnlyList<string> LoadOfficialJvmNames(string repoRoot, string listFileName)
    {
        var listPath = Path.Combine(NativeDataRoot(repoRoot), listFileName);
        return File.ReadAllLines(listPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split(',')[0].Trim())
            .ToList();
    }

    public static IReadOnlyList<MobFamilyPilot> LoadMobFamilyPilots(string repoRoot)
    {
        var listPath = Path.Combine(NativeDataRoot(repoRoot), "geometry_ir_mob_family_pilot_jvm.txt");
        var pilots = new List<MobFamilyPilot>();
        foreach (var line in File.ReadAllLines(listPath))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
            {
                continue;
            }

            var parts = t.Split(',');
            if (parts.Length < 4)
            {
                throw new InvalidDataException($"mob family pilot line needs 4 fields: {t}");
            }

            pilots.Add(new MobFamilyPilot(
                parts[0].Trim(),
                int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return pilots;
    }

    public static HashSet<string> LoadReferenceCuboidStrictSet(string repoRoot) =>
        new(LoadOfficialJvmNames(repoRoot, "geometry_ir_reference_cuboid_strict_jvm.txt"), StringComparer.Ordinal);

    public static HashSet<string> LoadAssemblyViewportStrictSet(string repoRoot) =>
        new(LoadOfficialJvmNames(repoRoot, "geometry_ir_assembly_viewport_strict_jvm.txt"), StringComparer.Ordinal);

    public static string ClientJarPath(string repoRoot, string versionLabel = MobFamilyPilotVersionLabel) =>
        Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "client.jar");

    public static bool IsClientJarPresent(string repoRoot, string versionLabel = MobFamilyPilotVersionLabel) =>
        File.Exists(ClientJarPath(repoRoot, versionLabel));

    public static (int W, int H) ResolveParityAtlasSize(string officialJvmName, JsonElement? geometryRoot = null)
    {
        if (geometryRoot is { } root &&
            root.TryGetProperty("textureWidth", out var tw) && tw.ValueKind == JsonValueKind.Number &&
            root.TryGetProperty("textureHeight", out var th) && th.ValueKind == JsonValueKind.Number)
        {
            return ((int)tw.GetDouble(), (int)th.GetDouble());
        }

        return officialJvmName switch
        {
            "net.minecraft.client.model.animal.fish.CodModel" => (32, 32),
            "net.minecraft.client.model.animal.fish.SalmonModel" => (32, 32),
            "net.minecraft.client.model.animal.chicken.AdultChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.chicken.ChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.chicken.BabyChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.cow.CowModel" => (64, 64),
            "net.minecraft.client.model.animal.cow.ColdCowModel" => (64, 64),
            "net.minecraft.client.model.animal.pig.PigModel" => (64, 64),
            "net.minecraft.client.model.ambient.BatModel" => (64, 64),
            "net.minecraft.client.model.monster.creeper.CreeperModel" => (64, 32),
            _ => (64, 64)
        };
    }

    public static bool RunLiftQualityIndexDiagnostics() =>
        string.Equals(
            Environment.GetEnvironmentVariable("AUTOPBR_RUN_LIFT_QUALITY_INDEX"),
            "1",
            StringComparison.Ordinal);

    /// <summary>T2 assembly viewport probes: assert legs-below-head on full pilot list (opt-in).</summary>
    public static bool RunAssemblyViewportProbeAssertions() =>
        string.Equals(
            Environment.GetEnvironmentVariable("AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES"),
            "1",
            StringComparison.Ordinal);

    /// <summary>T2: committed shard on disk but not yet ok — do not fail default CI.</summary>
    public static bool TryReadCommittedShardStatus(string shardPath, out string? extractionStatus)
    {
        extractionStatus = null;
        if (!File.Exists(shardPath))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        if (doc.RootElement.TryGetProperty("extractionStatus", out var st))
        {
            extractionStatus = st.GetString();
        }

        return true;
    }

    public readonly record struct MobFamilyPilot(
        string OfficialJvmName,
        int AtlasWidth,
        int AtlasHeight,
        int MinCuboids);
}
