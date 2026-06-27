using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class BytecodeGeometryMeshLiftTests
{
    [Fact]
    public void Disassembler_emits_addOrReplaceChild_binding_line()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        if (!ClientJarIO.TryResolveJarEntry(jar, "net.minecraft.client.model.animal.chicken.AdultChickenModel", null,
                out _, out var bytes))
        {
            return;
        }

        Assert.True(
            JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(bytes, "createBaseChickenModel", out var lines));
        Assert.Contains(lines, l => l.Contains("addOrReplaceChild", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("CubeListBuilder", StringComparison.Ordinal));
    }

    [Fact]
    public void Bytecode_mesh_resolution_finds_cod_factory()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar,
            maps: null,
            "net.minecraft.client.model.animal.fish.CodModel",
            "createBodyLayer",
            out var resolved));
        Assert.Contains("addOrReplaceChild", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) >= 6);
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (p["cuboids"] is JsonArray c)
                {
                    n += c.Count;
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return n;
    }
}
