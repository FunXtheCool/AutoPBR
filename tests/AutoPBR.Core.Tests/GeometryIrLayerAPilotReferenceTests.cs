using System.Text.Json;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>Layer A static rig pilots: reference_java vs committed IR (informational; not in strict list until promoted).</summary>
public sealed class GeometryIrLayerAPilotReferenceTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new(GeometryIrTestTierSupport.MobFamilyPilotVersionLabel, "unused", new Version(26, 1, 2));

    public static IEnumerable<object[]> PilotJvmCases() =>
        PilotJvms.Select(j => new object[] { j });

    public static readonly string[] PilotJvms =
    [
        "net.minecraft.client.model.animal.feline.AdultCatModel",
        "net.minecraft.client.model.animal.feline.BabyCatModel",
        "net.minecraft.client.model.animal.fox.AdultFoxModel",
        "net.minecraft.client.model.animal.fox.BabyFoxModel",
        "net.minecraft.client.model.animal.fox.FoxModel",
        "net.minecraft.client.model.animal.armadillo.ArmadilloModel",
        "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel",
        "net.minecraft.client.model.animal.armadillo.BabyArmadilloModel",
        "net.minecraft.client.model.monster.breeze.BreezeModel",
    ];

    [Theory]
    [MemberData(nameof(PilotJvmCases))]
    public void Reference_cuboids_align_when_ir_ok(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
        if (!string.Equals(ir.RootElement.GetProperty("extractionStatus").GetString(), "ok",
                StringComparison.Ordinal))
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.08);
        if (!StrictPilotJvm.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    private static readonly HashSet<string> StrictPilotJvm = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.animal.feline.AdultCatModel",
        "net.minecraft.client.model.animal.feline.BabyCatModel",
        "net.minecraft.client.model.animal.fox.AdultFoxModel",
        "net.minecraft.client.model.animal.fox.BabyFoxModel",
        "net.minecraft.client.model.animal.fox.FoxModel",
        "net.minecraft.client.model.animal.armadillo.ArmadilloModel",
        "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel",
        "net.minecraft.client.model.animal.armadillo.BabyArmadilloModel",
        "net.minecraft.client.model.monster.breeze.BreezeModel",
    };
}
