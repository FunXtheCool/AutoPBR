using AutoPBR.Tests.TestSupport;
using AutoPBR.Tools.GeometryCompiler;
using System.Text.Json.Nodes;
using System.Text.Json.Nodes; using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Agent 1A binding_gap pilots: entry factories with 0 bind lines must deep-concat host mesh
/// (<c>createBodyMesh</c>, <c>createBabyMesh</c>, saddle layers) and recover honest part trees.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed partial class BindingGapPilotMeshLiftTests
{
    public static TheoryData<string, string, string?, string, int> BindingGapPilotCases { get; } = new()
    {
        {
            "net.minecraft.client.model.animal.pig.PigModel",
            "createBodyLayer",
            "QuadrupedModel.createBodyMesh",
            "net.minecraft.client.model.animal.pig.PigModel",
            6
        },
        {
            "net.minecraft.client.model.animal.equine.HorseModel",
            "createBodyLayer",
            null,
            "net.minecraft.client.model.animal.equine.BabyHorseModel",
            6
        },
        {
            "net.minecraft.client.model.animal.armadillo.ArmadilloModel",
            "createBodyLayer",
            null,
            "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel",
            6
        },
        {
            "net.minecraft.client.model.animal.camel.CamelModel",
            "createBodyLayer",
            "createBodyMesh",
            "net.minecraft.client.model.animal.camel.AdultCamelModel",
            6
        },
        {
            "net.minecraft.client.model.animal.camel.CamelSaddleModel",
            "createSaddleLayer",
            "createBodyMesh",
            "net.minecraft.client.model.animal.camel.CamelSaddleModel",
            6
        },
        {
            "net.minecraft.client.model.animal.equine.EquineSaddleModel",
            "createSaddleLayer",
            "createBodyMesh",
            "net.minecraft.client.model.animal.equine.EquineSaddleModel",
            6
        },
        {
            "net.minecraft.client.model.animal.sheep.SheepModel",
            "createBodyLayer",
            "QuadrupedModel.createBodyMesh",
            "net.minecraft.client.model.animal.sheep.SheepModel",
            6
        },
        {
            "net.minecraft.client.model.animal.equine.DonkeyModel",
            "createBodyLayer",
            "MESH_TRANSFORMER_LAMBDA",
            "net.minecraft.client.model.animal.equine.DonkeyModel",
            6
        },
    };

    [Theory]
    [MemberData(nameof(BindingGapPilotCases))]
    public void Binding_gap_pilot_mesh_resolution_pulls_host_factory_bindings(
        string jvm,
        string factoryMethod,
        string? meshConcatNeedle,
        string expectedMeshHost,
        int minBindingLines)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, jvm, factoryMethod, out var resolved),
            $"mesh resolution failed for {jvm}");

        Assert.Equal(expectedMeshHost, resolved.HostJvmName);

        if (meshConcatNeedle is not null)
        {
            Assert.Contains(meshConcatNeedle, resolved.MeshConcat, StringComparison.Ordinal);
        }

        var bindingLines = resolved.MeshConcat
            .Split('\n')
            .Count(l => JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(l));
        Assert.True(bindingLines >= minBindingLines,
            $"expected >={minBindingLines} binding lines in concat for {jvm}, got {bindingLines}");

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        Assert.False(
            notes.Any(n => n.Contains("missing addChild", StringComparison.OrdinalIgnoreCase)),
            string.Join("; ", notes));

        var ids = CollectPartIds(roots);
        Assert.Contains("head", ids);
        Assert.Contains("body", ids);
        Assert.Contains("right_hind_leg", ids);
        Assert.True(ids.Count >= 6, $"expected head+body+4 legs, got [{string.Join(", ", ids)}]");
    }
}
