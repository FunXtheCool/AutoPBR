using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;
using System.Text.Json.Nodes; using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Wave 7 partial drain — promoted lifts, tree repair, and honest skip policy.</summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class Wave7PartialDrainTests
{
    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    [Fact]
    public void ColdChicken_head_has_two_overlay_cuboids()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.animal.chicken.ColdChickenModel", "createBodyLayer",
                preferAsm: true, out var attempt),
            string.Join("; ", attempt.Notes));

        var head = FindPartById(attempt.Roots, "head");
        Assert.NotNull(head);
        Assert.Equal(2, head!["cuboids"]!.AsArray().Count);
    }

    [Fact]
    public void ColdCow_right_horn_has_single_main_cuboid_after_tree_repair()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.animal.cow.ColdCowModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        var horn = FindPartById(attempt.Roots, "right_horn");
        Assert.NotNull(horn);
        Assert.Single(horn!["cuboids"]!.AsArray());
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.ColdCowModel")]
    [InlineData("net.minecraft.client.model.object.boat.BoatModel")]
    public void HonestSkip_marks_reference_mismatch_partials_as_skipped(string jvm)
    {
        var shard = new JsonObject
        {
            ["extractionStatus"] = "partial",
            ["extractionNotes"] = new JsonArray(),
            ["roots"] = new JsonArray { new JsonObject { ["id"] = "head", ["cuboids"] = new JsonArray() } },
        };

        GeometryIrPartialHonestSkip.ApplyIfStillPartial(jvm, shard);

        Assert.Equal("skipped", shard["extractionStatus"]!.GetValue<string>());
        Assert.Contains(shard["extractionNotes"]!.AsArray(),
            n => n!.GetValue<string>().Contains("reference", StringComparison.OrdinalIgnoreCase) ||
                 n.GetValue<string>().Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                 n.GetValue<string>().Contains("mesh", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("net.minecraft.client.model.object.boat.BoatModel", "createBoatModel")]
    [InlineData("net.minecraft.client.model.object.boat.RaftModel", "createRaftModel")]
    public void Boat_family_resolves_primary_factory_name(string jvm, string factory)
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var classBytes));
        Assert.Equal(factory, MeshFactoryMethodResolver.Resolve(null, jvm, "createBodyLayer", classBytes));
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
