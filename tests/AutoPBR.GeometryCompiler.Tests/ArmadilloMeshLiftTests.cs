using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Armadillo adult/baby factories use deep nested <c>PartDefinition</c> chains (head → ears → cubes; baby tail → ear).
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class ArmadilloMeshLiftTests
{
    [Fact]
    public void Adult_jar_lift_matches_javap_nested_head_and_ear_hierarchy()
    {
        const string jvm = "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel";
        Assert.True(TryLiftArmadillo(jvm, out var roots, out var notes), string.Join("; ", notes));

        Assert.True(PartNestedUnder(roots, "head_cube", "head"), "head_cube must nest under head");
        Assert.True(PartNestedUnder(roots, "right_ear", "head"), "right_ear must nest under head");
        Assert.True(PartNestedUnder(roots, "left_ear", "head"), "left_ear must nest under head");
        Assert.True(PartNestedUnder(roots, "right_ear_cube", "right_ear"), "right_ear_cube must nest under right_ear");
        Assert.True(PartNestedUnder(roots, "left_ear_cube", "left_ear"), "left_ear_cube must nest under left_ear");
        Assert.False(PartNestedUnder(roots, "left_hind_leg", "body"), "legs stay on getRoot()");
    }

    [Fact]
    public void Baby_jar_lift_nests_tail_ear_cube_and_head_stack_under_head_cube()
    {
        const string jvm = "net.minecraft.client.model.animal.armadillo.BabyArmadilloModel";
        Assert.True(TryLiftArmadillo(jvm, out var roots, out var notes), string.Join("; ", notes));

        Assert.True(PartNestedUnder(roots, "right_ear_cube", "tail"));
        Assert.True(PartNestedUnder(roots, "head_cube", "head"));
        Assert.True(PartNestedUnder(roots, "right_ear", "head_cube"));
        Assert.True(PartNestedUnder(roots, "left_ear", "head_cube"));
        Assert.False(PartNestedUnder(roots, "left_hind_leg", "body"), "legs stay on getRoot()");
    }

    private static bool TryLiftArmadillo(string jvm, out JsonArray roots, out List<string> notes)
    {
        roots = new JsonArray();
        notes = [];
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return false;
        }

        if (!BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved))
        {
            notes.Add($"mesh resolution failed for {jvm}");
            return false;
        }

        return JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out roots, out notes);
    }

    private static bool PartNestedUnder(JsonArray roots, string childId, string parentId)
    {
        foreach (var root in roots)
        {
            if (root is JsonObject rootObj && PartNestedUnderRecursive(rootObj, childId, parentId, currentParentId: null))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PartNestedUnderRecursive(JsonObject part, string childId, string parentId, string? currentParentId)
    {
        var id = (string?)part["id"];
        if (string.Equals(id, childId, StringComparison.Ordinal) &&
            string.Equals(currentParentId, parentId, StringComparison.Ordinal))
        {
            return true;
        }

        if (part["children"] is not JsonArray kids)
        {
            return false;
        }

        foreach (var child in kids)
        {
            if (child is JsonObject childObj &&
                PartNestedUnderRecursive(childObj, childId, parentId, id ?? currentParentId))
            {
                return true;
            }
        }

        return false;
    }
}
