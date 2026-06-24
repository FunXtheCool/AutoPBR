using System.Text.Json.Nodes;

using AutoPBR.Tools.GeometryCompiler;

using Xunit;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Cow <c>createBodyLayer</c> delegates to <c>createBaseCowModel</c>; concat must not duplicate factories or merge wrong islands.
/// </summary>
public sealed class CowDelegatedMeshLiftTests
{
    private const string CowFqn = "net.minecraft.client.model.animal.cow.CowModel";

    [Fact]
    public void CowModel_bytecode_lift_has_six_root_parts_and_ten_cuboids()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, CowFqn, "createBodyLayer", out var resolved),
            "mesh resolution failed");
        Assert.Contains("createBaseCowModel", resolved.MeshConcat, StringComparison.Ordinal);

        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes, officialJvmName: CowFqn),
            string.Join("; ", notes.Take(12)));

        var children = roots[0]!["children"]!.AsArray();
        var ids = children.OfType<JsonObject>().Select(o => (string?)o["id"]).Where(s => s is not null).OrderBy(s => s)
            .ToList();
        Assert.Equal(
            ["body", "head", "left_front_leg", "left_hind_leg", "right_front_leg", "right_hind_leg"],
            ids);
        Assert.Equal(10, CountCuboids(roots[0]!.AsObject()));
    }

    [Fact]
    public void CowModel_lifted_leg_poses_use_distinct_quadruped_offsets()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, CowFqn, "createBodyLayer", out var resolved));
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes, officialJvmName: CowFqn),
            string.Join("; ", notes.Take(12)));

        var legs = roots[0]!["children"]!.AsArray().OfType<JsonObject>()
            .Where(o => ((string?)o["id"] ?? "").Contains("leg", StringComparison.Ordinal))
            .ToDictionary(o => (string)o["id"]!);

        static (double X, double Y, double Z) T(JsonObject p) =>
            (p["pose"]!["translation"]![0]!.GetValue<double>(),
                p["pose"]!["translation"]![1]!.GetValue<double>(),
                p["pose"]!["translation"]![2]!.GetValue<double>());

        Assert.Equal((-4, 12, 7), T(legs["right_hind_leg"]));
        Assert.Equal((4, 12, 7), T(legs["left_hind_leg"]));
        Assert.Equal((-4, 12, -5), T(legs["right_front_leg"]));
        Assert.Equal((4, 12, -5), T(legs["left_front_leg"]));
    }

    private static int CountCuboids(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray cuboids ? cuboids.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids.OfType<JsonObject>())
            {
                n += CountCuboids(ch);
            }
        }

        return n;
    }
}
