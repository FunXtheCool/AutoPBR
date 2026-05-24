using System.Text.Json;


using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>Phase 6: humanoid Java reference_java bakes vs committed 26.1.2 IR shards (cuboids + poses).</summary>
public sealed class Phase6ReferenceAlignmentTests(ITestOutputHelper output)
{
    private static readonly string[] Phase6JvmNames =
    [
        "net.minecraft.client.model.player.PlayerModel",
        "net.minecraft.client.model.HumanoidModel",
    ];

    private static readonly HashSet<string> StrictCuboidAlignment = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.HumanoidModel",
        "net.minecraft.client.model.player.PlayerModel",
    };

    private static readonly HashSet<string> StrictCuboidAndPoseAlignment = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.HumanoidModel",
        "net.minecraft.client.model.player.PlayerModel",
    };

    [Theory]
    [MemberData(nameof(Phase6Cases))]
    public void Java_reference_json_is_present_for_humanoid_pilot(string jvm)
    {
        var root = FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        Assert.True(File.Exists(referencePath), $"missing reference bake: {jvm}");
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        Assert.Equal("reference_java", reference.RootElement.GetProperty("extractionStatus").GetString());
        Assert.Equal("createMesh", reference.RootElement.GetProperty("factoryMethod").GetString());
    }

    [Theory]
    [MemberData(nameof(Phase6Cases))]
    public void Java_reference_cuboids_align_with_ir_shard_when_present(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.08);
        if (!StrictCuboidAlignment.Contains(jvm))
        {
            return;
        }

        if (!cmp.IsMatch)
        {
            output.WriteLine($"{jvm}: {cmp.Message}");
        }

        Assert.True(cmp.IsMatch, cmp.Message ?? $"cuboids reference={cmp.ReferenceCuboids} ir={cmp.ComparedCuboids}");
    }

    [Theory]
    [MemberData(nameof(Phase6Cases))]
    public void Java_reference_cuboids_align_with_ir_shard_when_present_player_pilot(string jvm)
    {
        if (!string.Equals(jvm, "net.minecraft.client.model.player.PlayerModel", StringComparison.Ordinal))
        {
            return;
        }

        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.08);
        output.WriteLine(
            cmp.IsMatch
                ? $"{jvm}: cuboids aligned ({cmp.ReferenceCuboids})"
                : $"{jvm}: {cmp.Message}");
    }

    [Theory]
    [MemberData(nameof(Phase6Cases))]
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

    public static IEnumerable<object[]> Phase6Cases() => Phase6JvmNames.Select(j => new object[] { j });

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
