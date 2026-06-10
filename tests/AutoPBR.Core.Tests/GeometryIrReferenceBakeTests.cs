using System.Text.Json;
using AutoPBR.Tests.TestSupport;


namespace AutoPBR.Core.Tests;

/// <summary>
/// T2 probe + T1 strict reference alignment (allowlist: geometry_ir_reference_cuboid_strict_jvm.txt).
/// </summary>
public sealed class GeometryIrReferenceBakeTests
{
    private static readonly string[] PilotJvmNames =
        GeometryIrTestTierSupport.LoadMobFamilyPilots(GeometryIrTestTierSupport.FindRepoRoot())
            .Select(p => p.OfficialJvmName)
            .Concat(
            [
                "net.minecraft.client.model.monster.blaze.BlazeModel",
                "net.minecraft.client.model.monster.guardian.GuardianModel",
                "net.minecraft.client.model.player.PlayerModel",
                "net.minecraft.client.model.HumanoidModel",
            ])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static readonly HashSet<string> StrictReferenceIrAlignment =
        GeometryIrTestTierSupport.LoadReferenceCuboidStrictSet(GeometryIrTestTierSupport.FindRepoRoot());

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Java_reference_bake_aligns_with_ir_shard_when_present(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return;
        }

        if (GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var irStatus) &&
            !string.Equals(irStatus, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.TryGetProperty("extractionStatus", out var st) &&
            (st.GetString() is "reference_stub" or null))
        {
            return;
        }

        using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShard(reference.RootElement, ir.RootElement, tolerance: 0.05);
        if (!StrictReferenceIrAlignment.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message ?? $"reference={cmp.ReferenceCuboids} ir={cmp.ComparedCuboids}");
    }

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Java_reference_json_is_present_for_pilot(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        Assert.Equal("reference_java", reference.RootElement.GetProperty("extractionStatus").GetString());
        Assert.True(reference.RootElement.GetProperty("roots").GetArrayLength() > 0);
    }

    [Fact]
    public void Cod_java_reference_aligns_with_parity_mesh_emit()
    {
        const string jvm = "net.minecraft.client.model.animal.fish.CodModel";
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = CleanRoomEntityModelRuntime.TryBuildCodGeometryIrMeshForTests(
            "entity/fish/cod", profile, CleanRoomEntityModelRuntime.BabyProfile.Adult, tailSway: 0f, out _);
        Assert.NotNull(mesh);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(reference.RootElement, mesh, tolerance: 0.08);
        if (!StrictReferenceIrAlignment.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    public static IEnumerable<object[]> PilotCases() => PilotJvmNames.Select(j => new object[] { j });
}
