using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class NautilusBabyBodyLayerLiftTests
{
    [Fact]
    public void Nautilus_createBabyBodyLayer_lift_uses_baby_shell_uv_layout()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(
                JavapLocator.FindJavap(),
                jar,
                null,
                "net.minecraft.client.model.animal.nautilus.NautilusModel",
                "createBabyBodyLayer",
                preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        var shell = FindPartById(attempt.Roots, "shell");
        Assert.NotNull(shell);
        var cuboids = shell!["cuboids"]!.AsArray();
        Assert.True(cuboids.Count >= 2);
        Assert.Equal(0, cuboids[0]!["uvOrigin"]!.AsArray()[0]!.GetValue<int>());
        Assert.Equal(0, cuboids[0]!["uvOrigin"]!.AsArray()[1]!.GetValue<int>());
        Assert.Equal(0, cuboids[1]!["uvOrigin"]!.AsArray()[0]!.GetValue<int>());
        Assert.Equal(11, cuboids[1]!["uvOrigin"]!.AsArray()[1]!.GetValue<int>());

        var shardPath = Path.Combine(
            Program.FindRepoRoot(),
            "docs",
            "generated",
            "geometry",
            "26.1.2",
            "net.minecraft.client.model.animal.nautilus.NautilusModel.createBabyBodyLayer.json");
        Assert.True(File.Exists(shardPath), shardPath);
        var shard = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        Assert.Equal(64, shard["textureWidth"]!.GetValue<int>());
        Assert.Equal(64, shard["textureHeight"]!.GetValue<int>());
    }

    private static string ResolveClientJar()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(File.Exists(jar), $"Missing client.jar at {jar}");
        return jar;
    }

    private static JsonObject? FindPartById(JsonArray parts, string id)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                return o;
            }

            if (o["children"] is JsonArray ch)
            {
                var found = FindPartById(ch, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
