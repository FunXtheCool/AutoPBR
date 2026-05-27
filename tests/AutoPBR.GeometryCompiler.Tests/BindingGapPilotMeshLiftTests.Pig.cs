using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed partial class BindingGapPilotMeshLiftTests
{
    [Fact]
    public void PigModel_lift_includes_quadruped_leg_bindings_not_head_only()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(
                jar,
                null,
                "net.minecraft.client.model.animal.pig.PigModel",
                "createBodyLayer",
                out var resolved));

        Assert.Contains("createLegs", resolved.MeshConcat, StringComparison.Ordinal);

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        var head = FindPart(roots, "head");
        Assert.NotNull(head);
        Assert.Equal(2, head!["cuboids"]!.AsArray().Count);

        foreach (var legId in new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" })
        {
            var leg = FindPart(roots, legId);
            Assert.NotNull(leg);
            Assert.NotEmpty(leg!["cuboids"]!.AsArray());
        }
    }
}
