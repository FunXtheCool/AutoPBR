using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Phase 1A T0: pilots whose javap nests parts (e.g. head → beak, body → legs), not flat quadruped siblings.</summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class NestedHierarchyMeshLiftTests
{
    private const string AdultChickenJvm = "net.minecraft.client.model.animal.chicken.AdultChickenModel";

    public static TheoryData<string> NestedQuadrupedLegPilotJvms { get; } = new()
    {
        "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel",
        "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel",
        "net.minecraft.client.model.animal.equine.BabyDonkeyModel",
        "net.minecraft.client.model.animal.llama.BabyLlamaModel",
        "net.minecraft.client.model.animal.llama.LlamaModel",
        "net.minecraft.client.model.animal.rabbit.AdultRabbitModel",
        "net.minecraft.client.model.animal.rabbit.BabyRabbitModel",
        "net.minecraft.client.model.animal.rabbit.RabbitModel",
        "net.minecraft.client.model.animal.sniffer.SnifferModel",
        "net.minecraft.client.model.animal.sniffer.SniffletModel",
        "net.minecraft.client.model.animal.wolf.AdultWolfModel",
        "net.minecraft.client.model.animal.wolf.WolfModel",
        "net.minecraft.client.model.monster.dragon.EnderDragonModel",
    };

    /// <summary>Adult feline host mesh: legs bind on getRoot local after body; lifter nests under body (flatCount 0).</summary>
    public static TheoryData<string> FelineNestedQuadrupedLegPilotJvms { get; } = new()
    {
        "net.minecraft.client.model.animal.feline.AbstractFelineModel",
        "net.minecraft.client.model.animal.feline.AdultFelineModel",
        "net.minecraft.client.model.animal.feline.AdultCatModel",
        "net.minecraft.client.model.animal.feline.AdultOcelotModel",
        "net.minecraft.client.model.animal.feline.BabyOcelotModel",
    };

    [Fact]
    public void LlamaModel_javap_snapshot_nests_legs_under_body()
    {
        var root = Program.FindRepoRoot();
        var snapshotPath = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "javap-snapshots",
            "LlamaModel.createBodyLayer.javap.txt");
        if (!File.Exists(snapshotPath))
        {
            return;
        }

        var javap = File.ReadAllText(snapshotPath);
        Assert.True(JavapFloatGeometryMeshLift.TryLift(javap, out var roots, out var notes), string.Join("; ", notes));
        Assert.True(
            MeshLiftHierarchyTestSupport.LegsNestedUnderBody(roots),
            $"snapshot lift: {string.Join("; ", notes.Take(3))}");
    }

    [Fact]
    public void AdultChickenModel_jar_lift_nests_beak_under_head()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, AdultChickenJvm, "createBodyLayer", out var resolved),
            "mesh resolution failed");

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.False(
            notes.Any(n => n.Contains("No PartDefinition", StringComparison.Ordinal)),
            string.Join("; ", notes));

        var maxDepth = MeshLiftHierarchyTestSupport.MaxTreeDepth(roots);
        Assert.True(maxDepth >= 2, $"expected nested head→beak (depth>=2), got {maxDepth}");

        var head = MeshLiftHierarchyTestSupport.FindPartById(roots, "head");
        Assert.NotNull(head);
        var headKids = head!["children"]!.AsArray();
        Assert.Contains(
            headKids,
            n => n is JsonObject j && string.Equals((string?)j["id"], "beak", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(NestedQuadrupedLegPilotJvms))]
    public void Nested_pilot_jar_lift_nests_legs_under_body(string jvm)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var classBytes));
        var factoryMethod = MeshFactoryMethodResolver.Resolve(null, jvm, "createBodyLayer", classBytes);
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, jvm, factoryMethod, out var resolved),
            $"mesh resolution failed for {jvm} ({factoryMethod})");

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        Assert.True(
            MeshLiftHierarchyTestSupport.StandardQuadrupedLegsLiftedOffRoot(roots),
            $"{jvm}: expected four legs nested off mesh root (body/bone chain), notes={string.Join("; ", notes.Take(3))}");
        if (jvm.Contains("axolotl", StringComparison.Ordinal) ||
            jvm.Contains("rabbit", StringComparison.Ordinal) ||
            jvm.Contains("BabyDonkey", StringComparison.Ordinal) ||
            jvm.Contains(".llama.", StringComparison.Ordinal) ||
            jvm.Contains(".wolf.", StringComparison.Ordinal) ||
            jvm.Contains(".feline.", StringComparison.Ordinal))
        {
            Assert.True(
                MeshLiftHierarchyTestSupport.LegsNestedUnderBody(roots),
                $"{jvm}: expected four legs nested under body");
        }
    }

    [Fact]
    public void AdultFeline_createBodyMesh_snapshot_lift_nests_legs_under_body()
    {
        var root = Program.FindRepoRoot();
        var javapPath = Path.Combine(
            root,
            "tools",
            "minecraft-parity",
            "26.1.2",
            "javap-snapshots",
            "AdultFelineModel.createBodyMesh.javap.txt");
        if (!File.Exists(javapPath))
        {
            return;
        }

        var meshOnly = ExtractCreateBodyMeshBytecode(File.ReadAllLines(javapPath));
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(meshOnly, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(
            MeshLiftHierarchyTestSupport.LegsNestedUnderBody(roots),
            string.Join("; ", notes.Take(5)));
    }

    [Theory]
    [MemberData(nameof(FelineNestedQuadrupedLegPilotJvms))]
    public void Feline_pilot_jar_lift_nests_legs_under_body(string jvm) =>
        Nested_pilot_jar_lift_nests_legs_under_body(jvm);

    [Fact]
    public void BabyFelineModel_jar_lift_keeps_flat_root_legs()
    {
        const string jvm = "net.minecraft.client.model.animal.feline.BabyFelineModel";
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var classBytes));
        var factoryMethod = MeshFactoryMethodResolver.Resolve(null, jvm, "createBodyLayer", classBytes);
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, jvm, factoryMethod, out var resolved),
            $"mesh resolution failed for {jvm} ({factoryMethod})");

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        Assert.False(
            MeshLiftHierarchyTestSupport.LegsNestedUnderBody(roots),
            $"{jvm}: baby mesh binds legs before body — keep flat root siblings");
        foreach (var legId in new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" })
        {
            Assert.NotNull(MeshLiftHierarchyTestSupport.FindPartById(roots, legId));
        }
    }

    private static string ExtractCreateBodyMeshBytecode(string[] lines)
    {
        var take = new List<string>();
        var inMesh = false;
        foreach (var line in lines)
        {
            if (line.Contains("createBodyMesh", StringComparison.Ordinal) &&
                line.Contains("public static", StringComparison.Ordinal))
            {
                inMesh = true;
                continue;
            }

            if (inMesh && line.Contains("public void setupAnim", StringComparison.Ordinal))
            {
                break;
            }

            if (inMesh)
            {
                take.Add(line);
            }
        }

        return string.Join('\n', take);
    }
}
