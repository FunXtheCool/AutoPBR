using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// <c>HumanoidModel</c> / <c>PlayerModel</c> wide <c>createMesh</c> must lift without the
/// <c>clearRecursively</c> post-pass stripping in-factory shell clears.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class HumanoidPlayerMeshLiftTests
{
    private static readonly (string Jvm, string Method)[] Cases =
    [
        ("net.minecraft.client.model.HumanoidModel", "createMesh"),
        ("net.minecraft.client.model.player.PlayerModel", "createMesh"),
    ];

    [Theory]
    [MemberData(nameof(LiftCases))]
    public void Wide_createMesh_lifts_with_body_head_and_arms(string jvm, string factoryMethod)
    {
        var jar = Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, factoryMethod, out var resolved));
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(
                GeometryJavapLocator.FindJavap(),
                jar,
                null,
                jvm,
                factoryMethod,
                preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        Assert.True(CountCuboids(attempt.Roots) >= MinCuboidsFor(jvm), $"{jvm}: expected wide humanoid cuboids, got {CountCuboids(attempt.Roots)}");
        Assert.NotNull(FindPartById(attempt.Roots, "head"));
        Assert.NotNull(FindPartById(attempt.Roots, "body"));
        Assert.NotNull(FindPartById(attempt.Roots, "left_arm"));
        Assert.NotNull(FindPartById(attempt.Roots, "right_arm"));
    }

    public static IEnumerable<object[]> LiftCases() => Cases.Select(c => new object[] { c.Jvm, c.Method });

    private static int MinCuboidsFor(string jvm) =>
        string.Equals(jvm, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal) ? 6 : 8;

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var r in roots.OfType<JsonObject>())
        {
            n += CountPart(r);
        }

        return n;
    }

    private static int CountPart(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray c ? c.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids.OfType<JsonObject>())
            {
                n += CountPart(ch);
            }
        }

        return n;
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var r in roots.OfType<JsonObject>())
        {
            var found = FindPartByIdRecursive(r, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static JsonObject? FindPartByIdRecursive(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var ch in kids.OfType<JsonObject>())
        {
            var found = FindPartByIdRecursive(ch, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
