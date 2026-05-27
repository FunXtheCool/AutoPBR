using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed partial class BindingGapPilotMeshLiftTests
{
    [Fact]
    public void CamelSaddleModel_lift_includes_camel_body_and_saddle_overlay()
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
                "net.minecraft.client.model.animal.camel.CamelSaddleModel",
                "createSaddleLayer",
                out var resolved));

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        var ids = CollectPartIds(roots);
        Assert.Contains("saddle", ids);
        Assert.Contains("right_hind_leg", ids);
        Assert.True(ids.Count >= 10, $"expected full camel + tack, got [{string.Join(", ", ids)}]");
    }
}
