using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class QuadrupedLegDiagTests
{
    [Fact]
    public void Quadruped_javap_deep_concat_includes_legs()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(
            jar, "net.minecraft.client.model.QuadrupedModel", null, out _, out var quadBytes));
        var concat = BytecodeMeshResolution.BuildMeshConcatDeep(
            jar, null, "net.minecraft.client.model.QuadrupedModel", quadBytes, "createBodyMesh");
        Assert.Contains("createLegs", concat, StringComparison.Ordinal);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(concat, null, out var roots, out var notes),
            string.Join("; ", notes));
        var ids = CollectIds(roots);
        Assert.Contains(ids, id => string.Equals(id, "right_hind_leg", StringComparison.Ordinal));
    }

    private static List<string> CollectIds(System.Text.Json.Nodes.JsonArray roots)
    {
        var ids = new List<string>();
        void Walk(System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is System.Text.Json.Nodes.JsonObject p)
                {
                    ids.Add((string)p["id"]!);
                    if (p["children"] is System.Text.Json.Nodes.JsonArray ch)
                    {
                        Walk(ch);
                    }
                }
            }
        }

        Walk(roots);
        return ids;
    }
}
