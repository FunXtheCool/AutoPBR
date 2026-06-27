using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class QuadrupedMeshLiftTests
{
    [Fact]
    public void Quadruped_createBodyMesh_lift_has_body_and_four_legs()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, "net.minecraft.client.model.animal.cow.CowModel", "createBodyLayer", out var resolved));
        var concat = resolved.MeshConcat;
        Assert.Contains("right_hind_leg", concat, StringComparison.Ordinal);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(concat, null, out var roots, out var notes),
            string.Join("; ", notes));

        var ids = CollectPartIds(roots);
        Assert.Contains("body", ids);
        Assert.Contains("right_hind_leg", ids);
        var cuboidCount = CountCuboids(roots);
        Assert.True(cuboidCount >= 6, $"expected body+4 legs+head cuboids, got {cuboidCount} parts=[{string.Join(", ", ids)}] notes={string.Join("; ", notes.Take(3))}");
        Assert.True(ids.Count >= 5, $"expected head+body+4 legs, got {ids.Count}: [{string.Join(", ", ids)}]");
    }

    [Fact]
    public void QuadrupedModel_createBodyMesh_deep_concat_lifts_four_legs()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(
            jar, "net.minecraft.client.model.QuadrupedModel", null, out _, out var quadBytes));
        var concat = BytecodeMeshResolution.BuildMeshConcatDeep(
            jar, null, "net.minecraft.client.model.QuadrupedModel", quadBytes, "createBodyMesh");
        Assert.Contains("createLegs", concat, StringComparison.Ordinal);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(concat, null, out var roots, out var notes),
            string.Join("; ", notes));
        var ids = CollectPartIds(roots);
        Assert.Contains("right_hind_leg", ids);
        Assert.Contains("left_hind_leg", ids);
    }

    [Fact]
    public void QuadrupedModel_reference_java_cuboids_match_committed_ok_shard()
    {
        var root = Program.FindRepoRoot();
        const string jvm = "net.minecraft.client.model.QuadrupedModel";
        var referencePath = Path.Combine(
            root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return;
        }

        Assert.True(
            GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var status) &&
            string.Equals(status, "ok", StringComparison.Ordinal),
            $"{jvm} shard must be ok");

        using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvm, status!, ir.RootElement, root);
        Assert.True(entry.ReferenceCuboidsMatch, entry.ReferenceCompareMessage ?? jvm);
    }

    [Fact]
    public void Quadruped_createLegs_void_helper_lifts_four_legs()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, "net.minecraft.client.model.animal.cow.CowModel", "createBodyLayer", out var resolved));
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        var ids = CollectPartIds(roots);
        Assert.True(ids.Count >= 4, $"expected 4 legs, got {ids.Count}: [{string.Join(", ", ids)}] notes={string.Join("; ", notes)}");
    }

    private static List<string> CollectPartIds(JsonArray roots)
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
        return ids.OrderBy(s => s, StringComparer.Ordinal).ToList();
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (p["cuboids"] is JsonArray c)
                {
                    n += c.Count;
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return n;
    }
}
