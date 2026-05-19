using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class SaddleMeshLiftTests
{
    [Theory]
    [InlineData("net.minecraft.client.model.animal.camel.CamelSaddleModel", "createSaddleLayer")]
    [InlineData("net.minecraft.client.model.animal.equine.EquineSaddleModel", "createSaddleLayer")]
    public void Saddle_layer_factory_lifts_saddle_part(string jvmName, string factoryMethod)
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvmName, factoryMethod, out var resolved));
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.Contains("saddle", CollectPartIds(roots));
    }

    private static HashSet<string> CollectPartIds(System.Text.Json.Nodes.JsonArray roots)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Walk(System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not System.Text.Json.Nodes.JsonObject p)
                {
                    continue;
                }

                if (p["id"] is System.Text.Json.Nodes.JsonValue id)
                {
                    ids.Add(id.GetValue<string>()!);
                }

                if (p["children"] is System.Text.Json.Nodes.JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return ids;
    }
}
