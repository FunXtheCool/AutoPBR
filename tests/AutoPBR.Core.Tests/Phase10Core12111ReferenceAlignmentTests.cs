using System.Text.Json;


using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Phase 10: Java reference_java bakes vs committed 1.21.11 geometry IR (legacy profile pilots).
/// Strict cuboid asserts use a 1.21.11 subset only — see docs/test-guidance-geometry-animation-ir.md.
/// </summary>
public sealed class Phase10Core12111ReferenceAlignmentTests(ITestOutputHelper output)
{
    private static readonly MinecraftNativeProfile LegacyProfile =
        new("1.21.11", "unused", new Version(1, 21, 11));

    private static readonly string[] PilotJvmNames =
    [
        "net.minecraft.client.model.animal.chicken.ChickenModel",
        "net.minecraft.client.model.animal.cow.CowModel",
        "net.minecraft.client.model.animal.pig.PigModel",
        "net.minecraft.client.model.monster.creeper.CreeperModel",
    ];

    /// <summary>
    /// Models whose obfuscated IR still diverges from reference_java (pig: partial lift backlog on 1.21.11).
    /// </summary>
    private static readonly HashSet<string> StrictReferenceCuboidAlignment = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.animal.chicken.ChickenModel",
        "net.minecraft.client.model.animal.cow.CowModel",
        "net.minecraft.client.model.monster.creeper.CreeperModel",
    };

    private static readonly HashSet<string> ReferenceAlignmentExcluded = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.animal.pig.PigModel",
    };

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void GeometryIrDocumentLoader_loads_ok_shard_for_legacy_profile(string jvm)
    {
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(LegacyProfile, jvm, out var root));
        Assert.Equal("ok", root.GetProperty("extractionStatus").GetString());
        Assert.True(root.GetProperty("roots").GetArrayLength() > 0);
    }

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Java_reference_cuboids_align_with_1_21_11_ir_shard_when_present(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShard(reference.RootElement, ir.RootElement, tolerance: 0.08);
        if (ReferenceAlignmentExcluded.Contains(jvm))
        {
            output.WriteLine($"{jvm}: reference compare excluded (obfuscated lift backlog); {cmp.Message}");
            return;
        }

        if (!StrictReferenceCuboidAlignment.Contains(jvm))
        {
            output.WriteLine($"{jvm}: reference compare skipped (lift backlog); {cmp.Message}");
            return;
        }

        if (!cmp.IsMatch)
        {
            output.WriteLine($"{jvm}: {cmp.Message}");
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    public static IEnumerable<object[]> PilotCases() => PilotJvmNames.Select(j => new object[] { j });

    private static (JsonDocument? reference, JsonDocument? ir) LoadPair(string jvm)
    {
        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(LegacyProfile, jvm, out var irRoot))
        {
            return (null, null);
        }

        var root = FindRepoRoot();
        var referencePath = Path.Combine(
            root,
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return (null, null);
        }

        var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            reference.Dispose();
            return (null, null);
        }

        return (reference, JsonDocument.Parse(irRoot.GetRawText()));
    }

    private static string FindRepoRoot()
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
}
