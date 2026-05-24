using System.Text.Json;
using AutoPBR.Tests.Shared;


namespace AutoPBR.Core.Tests;

/// <summary>
/// T1: mob-family golden checks — lifted IR shards emit stable parity meshes (cuboid count + deterministic geometry).
/// Pilots: <c>geometry_ir_mob_family_pilot_jvm.txt</c> (see docs/test-guidance-geometry-animation-ir.md).
/// </summary>
public sealed class MobFamilyGeometryIrGoldenTests
{
    private static readonly IReadOnlyList<GeometryIrTestTierSupport.MobFamilyPilot> MobFamilies =
        GeometryIrTestTierSupport.LoadMobFamilyPilots(GeometryIrTestTierSupport.FindRepoRoot());

    public static IEnumerable<object[]> MobFamilyCases =>
        MobFamilies.Select(f => new object[] { f.OfficialJvmName, f.AtlasWidth, f.AtlasHeight, f.MinCuboids });

    [Theory]
    [MemberData(nameof(MobFamilyCases))]
    public void Ok_shard_emits_at_least_indexed_cuboid_count(
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        int minCuboids)
    {
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(profile, officialJvmName, out var root));
        Assert.True(CountCuboidsInPartTree(root) >= minCuboids);

        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            profile,
            officialJvmName,
            atlasWidth,
            atlasHeight,
            out var failure);
        Assert.Null(failure);
        Assert.NotNull(mesh);
        Assert.True(mesh.Elements.Count >= minCuboids,
            $"{officialJvmName}: emit={mesh.Elements.Count} shard={CountCuboidsInPartTree(root)}");
    }

    [Theory]
    [MemberData(nameof(MobFamilyCases))]
    public void Parity_emit_is_deterministic(
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        int _)
    {
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(profile, officialJvmName, out JsonElement _))
        {
            return;
        }

        var a = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", profile, officialJvmName, atlasWidth, atlasHeight, out var errA);
        var b = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", profile, officialJvmName, atlasWidth, atlasHeight, out var errB);
        Assert.Null(errA);
        Assert.Null(errB);
        Assert.NotNull(a);
        Assert.NotNull(b);

        var cmp = GeometryIrMeshParityComparer.Compare(a, b, tolerance: 1e-4f);
        Assert.True(cmp.IsMatch, cmp.Message ?? "meshes differ");
    }

    private static int CountCuboidsInPartTree(JsonElement root)
    {
        var n = 0;
        if (!root.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var part in roots.EnumerateArray())
        {
            n += CountCuboidsInPart(part);
        }

        return n;
    }

    private static int CountCuboidsInPart(JsonElement part)
    {
        var n = 0;
        if (part.TryGetProperty("cuboids", out var cuboids))
        {
            n += cuboids.GetArrayLength();
        }

        if (part.TryGetProperty("children", out var children))
        {
            foreach (var ch in children.EnumerateArray())
            {
                n += CountCuboidsInPart(ch);
            }
        }

        return n;
    }
}
