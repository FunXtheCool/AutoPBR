using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class SlimeMagmaCubeLiftTests
{
    [Fact]
    public void MagmaCubeModel_lift_assigns_per_segment_texOffs_from_loop_locals()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.monster.slime.MagmaCubeModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        var expected = new (int U, int V)[]
        {
            (0, 0), (0, 9), (0, 18), (0, 27), (32, 0), (32, 9), (32, 18), (32, 27)
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var part = FindPartById(attempt.Roots, $"cube{i}");
            Assert.NotNull(part);
            var uv = part!["cuboids"]!.AsArray()[0]!["uvOrigin"]!.AsArray();
            Assert.Equal(expected[i].U, uv[0]!.GetValue<int>());
            Assert.Equal(expected[i].V, uv[1]!.GetValue<int>());
        }
    }

    [Fact]
    public void SlimeModel_lift_retains_outer_body_shell_with_inner_face_parts()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.monster.slime.SlimeModel", "createOuterBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        Assert.NotNull(FindPartById(attempt.Roots, "outer_cube"));
        Assert.NotNull(FindPartById(attempt.Roots, "cube"));
        Assert.NotNull(FindPartById(attempt.Roots, "right_eye"));

        var outer = FindPartById(attempt.Roots, "outer_cube")!;
        var from = outer["cuboids"]!.AsArray()[0]!["from"]!.AsArray();
        var to = outer["cuboids"]!.AsArray()[0]!["to"]!.AsArray();
        Assert.Equal(-4, from[0]!.GetValue<int>());
        Assert.Equal(16, from[1]!.GetValue<int>());
        Assert.Equal(4, to[0]!.GetValue<int>());
        Assert.Equal(24, to[1]!.GetValue<int>());
    }

    private static string ResolveClientJar() =>
        GeometryIrTestTierSupport.TryClientJarPath(Program.FindRepoRoot())
        ?? throw new InvalidOperationException("Minecraft client jar not found.");

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
