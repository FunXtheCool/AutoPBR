using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class PartPoseIntStackLiftTests
{
    [Fact]
    public void Quadruped_createLegs_lifts_leg_pose_with_isub_and_scale_from_mesh_wide_ints()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, "net.minecraft.client.model.animal.pig.PigModel", "createBodyLayer", out var resolved));
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));

        var leg = FindPartById(roots, "right_hind_leg");
        Assert.NotNull(leg);
        var to = leg!["cuboids"]![0]!["to"]!.AsArray();
        Assert.Equal(6d, to[1]!.GetValue<double>(), 0.01);
    }

    [Fact]
    public void Pig_full_lift_leg_boxes_use_caller_scale_not_default_twelve()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, "net.minecraft.client.model.animal.pig.PigModel", "createBodyLayer", out var resolved));
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));

        var leg = FindPartById(roots, "right_hind_leg");
        Assert.NotNull(leg);
        var to = leg!["cuboids"]![0]!["to"]!.AsArray();
        Assert.Equal(6d, to[1]!.GetValue<double>(), 0.01);
    }

    private static JsonObject? FindPartById(JsonArray roots, string partId)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject rootPart)
            {
                continue;
            }

            var found = FindPartByIdRecursive(rootPart, partId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static JsonObject? FindPartByIdRecursive(JsonObject part, string partId)
    {
        if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var ch in kids)
        {
            if (ch is JsonObject co)
            {
                var found = FindPartByIdRecursive(co, partId);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
