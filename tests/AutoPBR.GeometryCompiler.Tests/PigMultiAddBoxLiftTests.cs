using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class PigMultiAddBoxLiftTests
{
    [Fact]
    public void Pig_createBasePigModel_lift_includes_both_head_cuboids()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        if (!ClientJarIO.TryResolveJarEntry(jar, "net.minecraft.client.model.animal.pig.PigModel", null, out _,
                out var bytes))
        {
            return;
        }

        var syn = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(bytes, ["createBasePigModel"], out _);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(syn, null, out var roots, out var notes),
            string.Join("; ", notes));

        var cuboids = 0;
        void Walk(System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not System.Text.Json.Nodes.JsonObject p)
                {
                    continue;
                }

                if (p["cuboids"] is System.Text.Json.Nodes.JsonArray c)
                {
                    cuboids += c.Count;
                }

                if (p["children"] is System.Text.Json.Nodes.JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        Assert.True(cuboids >= 2, $"expected head+snout cuboids, got {cuboids}");
    }

    [Fact]
    public void Pig_full_mesh_resolution_lifts_at_least_three_cuboids()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, "net.minecraft.client.model.animal.pig.PigModel", "createBodyLayer", out var resolved));
        Assert.Contains("createBasePigModel", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.Contains("createLegs", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.Contains("QuadrupedModel", resolved.MeshConcat, StringComparison.Ordinal);
        var islands = resolved.MeshConcat.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker,
            StringSplitOptions.RemoveEmptyEntries);
        Assert.True(islands.Length >= 3, $"expected multiple mesh islands, got {islands.Length}");

        ClientJarIO.TryResolveJarEntry(jar, "net.minecraft.client.model.QuadrupedModel", null, out _, out var quadBytes);
        var quadConcat = BytecodeMeshResolution.BuildMeshConcatDeep(
            jar, null, "net.minecraft.client.model.QuadrupedModel", quadBytes, "createBodyMesh");
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(quadConcat, null, out var quadRoots, out var quadNotes),
            string.Join("; ", quadNotes));
        var quadCuboids = CountCuboids(quadRoots);

        var legsSyn = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(quadBytes, ["createBodyMesh", "createLegs"], out _);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(legsSyn, null, out var legsRoots, out var legsNotes),
            string.Join("; ", legsNotes));
        var legsCuboids = CountCuboids(legsRoots);

        Assert.True(legsCuboids >= 1, $"bytecode createLegs+createBodyMesh={legsCuboids} quadDeep={quadCuboids}");

        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var asmRoots, out var notes),
            string.Join("; ", notes));

        var javapExe = JavapLocator.FindJavap();
        Assert.True(
            JavapClassDisassembly.TryDisassemble(javapExe, jar, resolved.HostJvmName, out var javapOut, out _));
        var javapConcat = JavapClassDisassembly.ConcatMeshFactoryCodeDeep(
            javapExe, jar, javapOut, resolved.HostJvmName, maps: null, resolved.HostJvmName);
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(javapConcat, out var javapRoots, out _, maps: null));

        var asmCuboids = CountCuboids(asmRoots);
        var javapCuboids = CountCuboids(javapRoots);
        if (javapCuboids >= 6 &&
            GeometryLiftCompareReport.AreStructurallyAligned(asmRoots, javapRoots, out var mismatch))
        {
            Assert.Fail(mismatch ?? "pig mesh lift misaligned");
        }

        var partIds = asmRoots[0]!["children"]!.AsArray()
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .Select(o => (string?)o["id"])
            .OrderBy(s => s)
            .ToList();
        Assert.True(asmCuboids >= 7, $"expected >=7 cuboids for pig reference parity, got {asmCuboids} parts=[{string.Join(", ", partIds)}]");
    }

    private static int CountCuboids(System.Text.Json.Nodes.JsonArray roots)
    {
        var n = 0;
        void Walk(System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not System.Text.Json.Nodes.JsonObject p)
                {
                    continue;
                }

                if (p["cuboids"] is System.Text.Json.Nodes.JsonArray c)
                {
                    n += c.Count;
                }

                if (p["children"] is System.Text.Json.Nodes.JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return n;
    }
}
