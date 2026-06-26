using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class BreezeWindLayerLiftTests
{
    [Fact]
    public void BreezeModel_lift_includes_wind_mid_cuboids()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.monster.breeze.BreezeModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var windMid = FindPartById(attempt.Roots, "wind_mid");
        Assert.NotNull(windMid);
        Assert.True(windMid["cuboids"]!.AsArray().Count >= 3);
    }

    [Fact]
    public void BreezeModel_lift_nests_wind_mid_under_wind_bottom()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.monster.breeze.BreezeModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var windBottom = FindPartById(attempt.Roots, "wind_bottom");
        Assert.NotNull(windBottom);
        var windMid = FindPartById(attempt.Roots, "wind_mid");
        Assert.NotNull(windMid);
        Assert.NotNull(FindPartById(attempt.Roots, "wind_top"));
        Assert.Contains(windBottom["children"]!.AsArray(),
            n => n is JsonObject o && string.Equals((string?)o["id"], "wind_mid", StringComparison.Ordinal));
    }

    [Fact]
    public void BreezeModel_lift_stamps_wind_cuboids_with_128_atlas_and_wind_texture_key()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.monster.breeze.BreezeModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        foreach (var partId in new[] { "wind_bottom", "wind_mid", "wind_top" })
        {
            var part = FindPartById(attempt.Roots, partId);
            Assert.NotNull(part);
            foreach (var cuboid in part["cuboids"]!.AsArray())
            {
                Assert.Equal(128, cuboid!["textureWidth"]!.GetValue<int>());
                Assert.Equal(128, cuboid["textureHeight"]!.GetValue<int>());
                Assert.Equal("#wind", (string?)cuboid["textureKey"]);
            }
        }
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
