using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;
using System.Text.Json.Nodes; using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Mesh-wide int/float maps must not use constants from later islands when lifting delegated createMesh.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class MeshWidePrefixScopeTests
{
    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    [Fact]
    public void HumanoidModel_createMesh_lift_includes_right_arm()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.HumanoidModel", null, out _, out var bytes));
        Assert.True(BytecodeGeometryMeshLift.TryLift(bytes, "createMesh", null, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.Contains("right_arm", CollectPartIds(roots));
    }

    [Fact]
    public void AbstractPiglin_bytecode_resolve_uses_AdultPiglin_host()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AbstractPiglinModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        Assert.Equal("net.minecraft.client.model.monster.piglin.AdultPiglinModel", resolved.HostJvmName);
    }

    [Fact]
    public void AdultPiglin_lift_includes_right_arm_left_pants_and_hat()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        Assert.NotNull(FindPartById(roots, "right_arm"));
        Assert.NotNull(FindPartById(roots, "left_pants"));
        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        var hat = FindPartById(roots, "hat");
        Assert.NotNull(hat);
        Assert.Empty(hat!["cuboids"]!.AsArray());
        AssertChildPartId(head["children"]!.AsArray(), "left_ear");
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject root)
            {
                continue;
            }

            var found = Walk(root, id);
            if (found is not null)
            {
                return found;
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

        foreach (var ch in kids)
        {
            if (ch is JsonObject co && Walk(co, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static void AssertChildPartId(JsonArray parts, string id) =>
        Assert.Contains(parts, n => n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal));

    private static List<string> CollectPartIds(JsonArray roots)
    {
        var ids = new List<string>();
        foreach (var n in roots)
        {
            if (n is JsonObject o)
            {
                WalkIds(o, ids);
            }
        }

        return ids;
    }

    private static void WalkIds(JsonObject part, List<string> ids)
    {
        if (part["id"]?.GetValue<string>() is { } id)
        {
            ids.Add(id);
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    WalkIds(co, ids);
                }
            }
        }
    }
}
