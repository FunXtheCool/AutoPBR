using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class ClimatePartialLiftTests
{
    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    [Fact]
    public void ColdCow_head_nests_horns_and_keeps_two_skin_cuboids()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        Assert.NotNull(FindPartById(head!["children"]!.AsArray(), "right_horn"));
        Assert.Equal(2, head!["cuboids"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.ColdCowModel", "body", 3)]
    [InlineData("net.minecraft.client.model.animal.cow.WarmCowModel", "head", 6)]
    [InlineData("net.minecraft.client.model.animal.pig.ColdPigModel", "body", 2)]
    public void Climate_variant_lift_matches_reference_part_cuboid_count(string jvm, string partId, int expected)
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var part = FindPartById(roots, partId);
        Assert.NotNull(part);
        Assert.Equal(expected, part!["cuboids"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.chicken.ColdChickenModel", "head", 2)]
    public void Climate_variant_lift_includes_reference_head_cuboid_count(string jvm, string partId, int expected)
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var part = FindPartById(roots, partId);
        Assert.NotNull(part);
        var count = part!["cuboids"]!.AsArray().Count;
        Assert.True(count == expected, $"{jvm}: head cuboids expected {expected}, got {count}");
    }

    private static JsonObject? FindRootChildPart(JsonArray roots, string id)
    {
        if (roots.Count == 0 || roots[0] is not JsonObject root || root["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var n in kids)
        {
            if (n is JsonObject o && string.Equals(o["id"]?.GetValue<string>(), id, StringComparison.Ordinal))
            {
                return o;
            }
        }

        return null;
    }

    private static JsonObject? FindPartById(JsonArray roots, string id) =>
        FindPartById((JsonNode?)roots, id);

    private static JsonObject? FindPartById(JsonNode? node, string id)
    {
        if (node is JsonObject o)
        {
            var found = Walk(o, id);
            if (found is not null)
            {
                return found;
            }
        }

        if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                var found = FindPartById(child, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static JsonObject? Walk(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var child in kids)
        {
            if (child is JsonObject co)
            {
                var found = Walk(co, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
