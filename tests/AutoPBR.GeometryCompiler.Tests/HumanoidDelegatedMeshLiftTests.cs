using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Humanoid <c>createMesh</c> delegation: deep concat must prepend the delegate island and must not re-append it last
/// (which would let default arm/head cuboids override host <c>createBodyLayer</c> <c>addOrReplaceChild</c> overrides).
/// </summary>
public sealed class HumanoidDelegatedMeshLiftTests
{
    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    public static IEnumerable<object[]> HumanoidDelegateJvmCases() =>
    [
        ["net.minecraft.client.model.monster.enderman.EndermanModel", "right_arm", -1.0, 30.0, false],
        ["net.minecraft.client.model.monster.skeleton.SkeletonModel", "right_arm", -1.0, 12.0, true],
        ["net.minecraft.client.model.monster.skeleton.BoggedModel", "right_arm", -1.0, 12.0, true],
    ];

    [Theory]
    [MemberData(nameof(HumanoidDelegateJvmCases))]
    public void Delegated_humanoid_mesh_concat_orders_createMesh_before_host_layer(
        string jvm, string partId, double armX, double armHeight, bool expectsVoidSkeletonMeshHelper)
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        var islands = resolved.MeshConcat
            .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        Assert.True(islands.Count >= 2, "expected delegated createMesh island before host createBodyLayer");
        Assert.Contains("HumanoidModel.createMesh", islands[0], StringComparison.Ordinal);
        Assert.Contains("addOrReplaceChild", resolved.MeshConcat, StringComparison.Ordinal);
        if (expectsVoidSkeletonMeshHelper)
        {
            Assert.Contains("createDefaultSkeletonMesh", resolved.MeshConcat, StringComparison.Ordinal);
            var voidIslandIdx = islands.FindIndex(i =>
                i.Contains("__AUTOPBR_VOID_MESH_HELPER__", StringComparison.Ordinal));
            Assert.True(voidIslandIdx >= 0, "void skeleton mesh helper island missing from concat");
            Assert.Equal(islands.Count - 1, voidIslandIdx);
        }

        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));

        var arm = FindPartById(roots, partId);
        Assert.NotNull(arm);
        var cuboid = arm["cuboids"]!.AsArray()[0]!.AsObject();
        var from = cuboid["from"]!.AsArray();
        var to = cuboid["to"]!.AsArray();
        Assert.InRange(from[0]!.GetValue<double>(), armX - 0.01, armX + 0.01);
        Assert.InRange(to[1]!.GetValue<double>() - from[1]!.GetValue<double>(), armHeight - 0.01, armHeight + 0.01);
    }

    [Fact]
    public void Skeleton_createDefaultSkeletonMesh_island_lifts_thin_arm_alone()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string skeleton = "net.minecraft.client.model.monster.skeleton.SkeletonModel";
        Assert.True(ClientJarIO.TryResolveJarEntry(jar, skeleton, null, out _, out var bytes));
        var block = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(bytes, ["createDefaultSkeletonMesh"], out var ok);
        Assert.True(ok);
        Assert.Contains("right_arm", block, StringComparison.Ordinal);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(block, null, out var roots, out var notes),
            string.Join("; ", notes));
        var arm = FindPartById(roots, "right_arm");
        Assert.NotNull(arm);
        var from = arm["cuboids"]![0]!["from"]!.AsArray();
        var to = arm["cuboids"]![0]!["to"]!.AsArray();
        Assert.InRange(to[1]!.GetValue<double>() - from[1]!.GetValue<double>(), 11.9, 12.1);
    }

    [Fact]
    public void AbstractZombieModel_mesh_host_lifts_two_head_cuboids()
    {
        const string jvm = "net.minecraft.client.model.monster.zombie.AbstractZombieModel";
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        Assert.Equal("net.minecraft.client.model.monster.zombie.BabyZombieModel", resolved.HostJvmName);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));

        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        Assert.Equal(2, head["cuboids"]!.AsArray().Count);
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var root in roots.OfType<JsonObject>())
        {
            var found = FindPartByIdRecursive(root, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static JsonObject? FindPartByIdRecursive(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var child in kids.OfType<JsonObject>())
        {
            var found = FindPartByIdRecursive(child, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
