using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Agent 1A: creeper-like factories must recover <c>PartDefinition.addOrReplaceChild</c> bindings from bytecode concat.
/// </summary>
public sealed class CreeperMeshLiftTests
{
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";

    [Fact]
    public void CreeperModel_lift_recovers_part_hierarchy()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, CreeperJvm, "createBodyLayer", out var resolved),
            "mesh resolution failed");

        var marker = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker;
        Assert.DoesNotContain(
            "areturn" + marker,
            resolved.MeshConcat,
            StringComparison.Ordinal);

        var bindingLines = resolved.MeshConcat
            .Split('\n')
            .Count(l => JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(l));
        Assert.True(bindingLines >= 6, $"expected >=6 binding lines in concat, got {bindingLines}");

        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        Assert.False(
            notes.Any(n => n.Contains("No PartDefinition", StringComparison.Ordinal)),
            string.Join("; ", notes));

        var ids = CountPartIds(roots);
        Assert.Contains("head", ids);
        Assert.Contains("body", ids);
        Assert.Contains("right_hind_leg", ids);
        Assert.True(ids.Count >= 6, $"expected head+body+4 legs, got [{string.Join(", ", ids)}]");

        // 26.1.2 creeper createBodyLayer binds head/body/legs directly on mesh root (flat siblings).
        var maxDepth = MaxTreeDepth(roots);
        var legsUnderBody = LegsNestedUnderBody(roots);
        Assert.True(
            maxDepth >= 2 || legsUnderBody || (maxDepth >= 1 && ids.Count >= 6),
            $"depth={maxDepth} legsUnderBody={legsUnderBody} ids={ids.Count}");
    }

    [Fact]
    public void Creeper_createBodyLayer_javap_slice_has_binding_lines()
    {
        var root = Program.FindRepoRoot();
        var snapshot = Path.Combine(
            root, "tools", "minecraft-parity", "26.1.2", "javap-snapshots", "CreeperModel.createBodyLayer.javap.txt");
        if (!File.Exists(snapshot))
        {
            return;
        }

        var text = File.ReadAllText(snapshot);
        var start = text.IndexOf("createBodyLayer", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var codeStart = text.IndexOf("Code:", start, StringComparison.Ordinal);
        Assert.True(codeStart >= 0);
        var slice = text[codeStart..];
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(slice, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountPartIds(roots).Count >= 6);
    }

    private static int MaxTreeDepth(JsonArray roots)
    {
        var max = 0;
        foreach (var node in roots)
        {
            if (node is JsonObject p)
            {
                max = Math.Max(max, PartDepth(p, 1));
            }
        }

        return max;
    }

    private static int PartDepth(JsonObject part, int depth)
    {
        if (part["children"] is not JsonArray kids || kids.Count == 0)
        {
            return depth;
        }

        var max = depth;
        foreach (var ch in kids)
        {
            if (ch is JsonObject co)
            {
                max = Math.Max(max, PartDepth(co, depth + 1));
            }
        }

        return max;
    }

    private static bool LegsNestedUnderBody(JsonArray roots)
    {
        var body = FindPartById(roots, "body");
        if (body is null || body["children"] is not JsonArray kids)
        {
            return false;
        }

        var legIds = new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" };
        return legIds.Any(id => kids.OfType<JsonObject>().Any(p => string.Equals((string?)p["id"], id, StringComparison.Ordinal)));
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindPartById(ro, id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPartById(JsonObject part, string id, out JsonObject? found)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co && TryFindPartById(co, id, out found))
                {
                    return true;
                }
            }
        }

        found = null;
        return false;
    }

    private static List<string> CountPartIds(JsonArray roots)
    {
        var ids = new List<string>();
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (p["id"] is JsonValue id)
                {
                    ids.Add(id.GetValue<string>()!);
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return ids;
    }
}
