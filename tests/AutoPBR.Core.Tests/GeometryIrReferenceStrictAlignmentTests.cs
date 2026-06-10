using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// T1: fingerprint alignment between Java reference_java bakes and committed 26.1.2 IR shards.
/// </summary>
public sealed class GeometryIrReferenceStrictAlignmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    public static IEnumerable<object[]> StrictJvmCases() =>
        GeometryIrTestTierSupport.LoadOfficialJvmNames(
                GeometryIrTestTierSupport.FindRepoRoot(),
                "geometry_ir_reference_cuboid_strict_jvm.txt")
            .Select(j => new object[] { j });

    [Theory]
    [MemberData(nameof(StrictJvmCases))]
    public void Reference_java_cuboids_align_with_committed_ir_shard(string jvm)
    {
        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(Profile26, jvm, out var irRoot))
        {
            return;
        }

        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(
            root,
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, irRoot, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }
}
