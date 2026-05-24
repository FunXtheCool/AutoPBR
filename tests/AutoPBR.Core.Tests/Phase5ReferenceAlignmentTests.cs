using System.Text.Json;


using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>Phase 5: Java reference_java bakes vs committed 26.1.2 IR shards (cuboids + poses).</summary>
public sealed class Phase5ReferenceAlignmentTests(ITestOutputHelper output)
{
    private static readonly string[] Phase5JvmNames =
    [
        "net.minecraft.client.model.monster.blaze.BlazeModel",
        "net.minecraft.client.model.monster.guardian.GuardianModel",
        "net.minecraft.client.model.animal.squid.BabySquidModel",
    ];

    private static readonly HashSet<string> StrictCuboidAndPoseAlignment = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.monster.blaze.BlazeModel",
        "net.minecraft.client.model.monster.guardian.GuardianModel",
        "net.minecraft.client.model.animal.squid.BabySquidModel",
    };

    [Theory]
    [MemberData(nameof(Phase5Cases))]
    public void Java_reference_cuboids_align_with_ir_shard_when_present(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShard(reference.RootElement, ir.RootElement, tolerance: 0.08);
        if (!StrictCuboidAndPoseAlignment.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message ?? $"cuboids reference={cmp.ReferenceCuboids} ir={cmp.ComparedCuboids}");
    }

    [Theory]
    [MemberData(nameof(Phase5Cases))]
    public void Java_reference_poses_align_with_ir_shard_when_present(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardWithPoses(
            reference.RootElement, ir.RootElement, cuboidTolerance: 0.08, poseTolerance: 0.08);
        if (!StrictCuboidAndPoseAlignment.Contains(jvm))
        {
            return;
        }

        if (!cmp.IsMatch)
        {
            output.WriteLine($"{jvm}: {cmp.Message}");
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    public static IEnumerable<object[]> Phase5Cases() => Phase5JvmNames.Select(j => new object[] { j });

    private static (JsonDocument? reference, JsonDocument? ir) LoadPair(string jvm)
    {
        var root = FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return (null, null);
        }

        var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            reference.Dispose();
            return (null, null);
        }

        return (reference, JsonDocument.Parse(File.ReadAllText(irPath)));
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
