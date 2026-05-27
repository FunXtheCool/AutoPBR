using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

/// <summary>
/// T1: parity vs viewport emit fidelity for mob-family pilots (geometry_ir_mob_family_pilot_jvm.txt).
/// </summary>
public sealed class MobFamilyGeometryIrFidelityTests
{
    private static readonly GeometryIrTestTierSupport.MobFamilyPilot[] FidelityPilots =
        GeometryIrTestTierSupport.LoadMobFamilyPilots(GeometryIrTestTierSupport.FindRepoRoot())
            .Where(p => p.OfficialJvmName is
                "net.minecraft.client.model.animal.fish.CodModel" or
                "net.minecraft.client.model.animal.fish.SalmonModel" or
                "net.minecraft.client.model.animal.pig.PigModel" or
                "net.minecraft.client.model.animal.cow.CowModel" or
                "net.minecraft.client.model.ambient.BatModel")
            .ToArray();

    public static IEnumerable<object[]> PilotCases =>
        FidelityPilots.Select(p => new object[] { p.OfficialJvmName, p.AtlasWidth, p.AtlasHeight });

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Parity_emit_matches_reference_mesh_when_reference_present(
        string officialJvmName, int atlasW, int atlasH)
    {
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(profile, officialJvmName, out _))
        {
            return;
        }

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output",
            $"{officialJvmName}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        var parity = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", profile, officialJvmName, atlasW, atlasH, out var parityErr);
        Assert.Null(parityErr);
        Assert.NotNull(parity);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(
            reference.RootElement, parity, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Cod_viewport_thickens_zero_extent_fin_while_parity_does_not()
    {
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        var p = CleanRoomEntityModelRuntime.BabyProfile.Adult;
        var parity = CleanRoomEntityModelRuntime.TryBuildCodGeometryIrMeshForTests(
            "entity/fish/cod", profile, p, tailSway: 0f, out _);
        var viewport = CleanRoomEntityModelRuntime.BuildAquatic(
            "entity/fish/cod", profile, isBaby: false, tailSway: 0f);
        Assert.NotNull(parity);
        Assert.True(HasElementWithLocalExtents(
            viewport, x0: -2f, y0: -0.08f, z0: -1f, x1: 0f, y1: 0.08f, z1: 1f, tol: 1e-3f));
        Assert.False(HasElementWithLocalExtents(
            parity, x0: -2f, y0: -0.08f, z0: -1f, x1: 0f, y1: 0.08f, z1: 1f, tol: 1e-3f));
    }

    [Fact]
    public void Generic_viewport_emit_thickens_salmon_zero_extent_fins_without_changing_parity_emit()
    {
        const string salmonJvm = "net.minecraft.client.model.animal.fish.SalmonModel";
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        var p = CleanRoomEntityModelRuntime.BabyProfile.Adult;
        var parity = CleanRoomEntityModelRuntime.TryBuildSalmonGeometryIrMeshForTests(
            "entity/fish/salmon", profile, p, tailSway: 0f, out var parityFailure);
        var viewport = CleanRoomEntityModelRuntime.TryBuildGeometryIrViewportMeshForTests(
            "entity/fish/salmon",
            profile,
            salmonJvm,
            atlasWidth: 32,
            atlasHeight: 32,
            out var viewportFailure);

        Assert.Null(parityFailure);
        Assert.Null(viewportFailure);
        Assert.NotNull(parity);
        Assert.NotNull(viewport);
        Assert.True(CountDegenerateXElements(parity!) > 0);
        Assert.Equal(0, CountDegenerateXElements(viewport!));
    }

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Packaged_preview_delta_loads_when_present(string officialJvmName, int atlasW, int atlasH)
    {
        _ = (atlasW, atlasH);
        var profile = new MinecraftNativeProfile(
            GeometryIrTestTierSupport.MobFamilyPilotVersionLabel,
            "unused",
            new Version(26, 1, 2));
        if (!GeometryIrPreviewDeltaDocument.TryLoad(profile, officialJvmName, out var delta))
        {
            return;
        }

        Assert.Equal(officialJvmName, delta.GetProperty("officialJvmName").GetString());
        Assert.True(GeometryIrPreviewDeltaDocument.HasDeltaKind(delta, "previewBasis"));
    }

    private static bool HasElementWithLocalExtents(
        MergedJavaBlockModel mesh,
        float x0,
        float y0,
        float z0,
        float x1,
        float y1,
        float z1,
        float tol)
    {
        foreach (var e in mesh.Elements)
        {
            if (MathF.Abs(e.From[0] - x0) <= tol && MathF.Abs(e.From[1] - y0) <= tol &&
                MathF.Abs(e.From[2] - z0) <= tol && MathF.Abs(e.To[0] - x1) <= tol &&
                MathF.Abs(e.To[1] - y1) <= tol && MathF.Abs(e.To[2] - z1) <= tol)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDegenerateXElements(MergedJavaBlockModel mesh)
    {
        var count = 0;
        foreach (var e in mesh.Elements)
        {
            if (MathF.Abs(e.To[0] - e.From[0]) <= 1e-5f)
            {
                count++;
            }
        }

        return count;
    }
}
