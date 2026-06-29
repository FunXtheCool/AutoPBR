using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class BabyPigLiftDiagnosticTests
{
    private const string BabyPigJvm = "net.minecraft.client.model.animal.pig.BabyPigModel";

    [Fact]
    public void BabyPig_mesh_resolution_uses_inline_baby_legs_not_quadruped_createLegs()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, BabyPigJvm, "createBodyLayer", out var resolved));
        Assert.DoesNotContain("QuadrupedModel.createLegs", resolved.MeshConcat, StringComparison.Ordinal);

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        var leg = FindPart(roots, "right_front_leg");
        Assert.NotNull(leg);
        var cuboid = leg!["cuboids"]!.AsArray()[0]!.AsObject();
        var from = cuboid["from"]!.AsArray();
        var to = cuboid["to"]!.AsArray();
        Assert.Equal(-1, from[0]!.GetValue<double>(), 3);
        Assert.Equal(1, to[0]!.GetValue<double>(), 3);
        Assert.Equal(2, to[1]!.GetValue<double>(), 3);
    }

    private static JsonObject? FindPart(JsonArray roots, string id)
    {
        JsonObject? found = null;
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (string.Equals((string?)p["id"], id, StringComparison.Ordinal))
                {
                    found = p;
                    return;
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return found;
    }
}
