using AutoPBR.Tests.TestSupport;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Ensures bytecode disassembly lift stays aligned with javap reference lift on real client.jar models.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class DualLiftRegressionTests
{
    private static readonly (string Jvm, string Method)[] PilotModels =
    [
        ("net.minecraft.client.model.animal.fish.CodModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.fish.SalmonModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.chicken.AdultChickenModel", "createBaseChickenModel"),
        ("net.minecraft.client.model.animal.chicken.ChickenModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.cow.CowModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.pig.PigModel", "createBodyLayer"),
        ("net.minecraft.client.model.monster.creeper.CreeperModel", "createBodyLayer"),
        ("net.minecraft.client.model.ambient.BatModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.axolotl.AdultAxolotlModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.axolotl.BabyAxolotlModel", "createBodyLayer"),
        ("net.minecraft.client.model.HumanoidModel", "createMesh"),
        ("net.minecraft.client.model.player.PlayerCapeModel", "createCapeLayer"),
        ("net.minecraft.client.model.player.PlayerModel", "createMesh"),
        ("net.minecraft.client.model.monster.blaze.BlazeModel", "createBodyLayer"),
        ("net.minecraft.client.model.monster.guardian.GuardianModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.squid.BabySquidModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.frog.FrogModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.sniffer.SnifferModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.goat.GoatModel", "createBodyLayer"),
        ("net.minecraft.client.model.monster.piglin.AdultPiglinModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.fox.AdultFoxModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.fox.BabyFoxModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.armadillo.ArmadilloModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.feline.AdultCatModel", "createBodyLayer"),
        ("net.minecraft.client.model.animal.feline.BabyCatModel", "createBodyLayer"),
    ];

    [Theory]
    [MemberData(nameof(PilotModelCases))]
    public void Bytecode_lift_aligns_with_javap_reference(string jvm, string factoryMethod)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        if (!BytecodeMeshResolution.TryResolve(jar, maps: null, jvm, factoryMethod, out var resolved))
        {
            return;
        }

        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var asmRoots, out _),
            $"bytecode lift failed for {jvm}");

        // Compare segment parser on the same deep mesh concat (bytecode-sourced); javap stdout concat can diverge for delegated factories.
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var javapRoots, out _, maps: null),
            $"segment lift failed for {jvm}");

        Assert.True(
            GeometryLiftCompareReport.AreStructurallyAligned(asmRoots, javapRoots, out var mismatch),
            $"{jvm}: {mismatch}");
    }

    public static IEnumerable<object[]> PilotModelCases() =>
        PilotModels.Select(t => new object[] { t.Jvm, t.Method });
}
