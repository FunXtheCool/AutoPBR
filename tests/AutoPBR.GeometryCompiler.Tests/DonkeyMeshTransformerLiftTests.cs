using System.Text.Json.Nodes;
using AutoPBR.Core.Preview;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class DonkeyMeshTransformerLiftTests
{
    private const string DonkeyJvm = "net.minecraft.client.model.animal.equine.DonkeyModel";

    private static string? ClientJar
    {
        get
        {
            var root = Program.FindRepoRoot();
            var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
            return File.Exists(jar) ? jar : null;
        }
    }

    [Fact]
    public void Donkey_createBodyLayer_deep_concat_includes_chest_parts()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        Assert.True(ClientJarIO.TryResolveJarEntry(jar, DonkeyJvm, null, out _, out var donkeyBytes));
        var modifyMeshBlock = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(
            donkeyBytes, ["modifyMesh"], out var modifyOk);
        Assert.True(modifyOk, "modifyMesh should disassemble");
        Assert.Contains("left_chest", modifyMeshBlock, StringComparison.Ordinal);
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftConcat(modifyMeshBlock, null, out var modifyRoots, out var modifyNotes),
            string.Join("; ", modifyNotes));
        var modifyIds = CollectPartIds(modifyRoots);
        Assert.Contains("left_chest", modifyIds);
        Assert.Contains("right_chest", modifyIds);

        Assert.True(BytecodeMeshResolution.TryResolve(
            jar, null, DonkeyJvm, "createBodyLayer", out var resolved));
        Assert.Contains("MESH_TRANSFORMER_LAMBDA", resolved.MeshConcat, StringComparison.Ordinal);
    }

    [Fact]
    public void Donkey_lifted_shard_has_fifteen_parts_and_matches_reference_cuboids()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        var root = Program.FindRepoRoot();
        var refPath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output",
            $"{DonkeyJvm}.json");
        Assert.True(File.Exists(refPath), $"missing reference bake: {DonkeyJvm}");

        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, DonkeyJvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var partIds = CollectPartIds(roots);
        Assert.Equal(15, partIds.Count);
        Assert.Contains("left_chest", partIds);
        Assert.Contains("right_chest", partIds);

        using var irDoc = System.Text.Json.JsonDocument.Parse(new JsonObject
        {
            ["schemaVersion"] = 2,
            ["officialJvmName"] = DonkeyJvm,
            ["extractionStatus"] = "ok",
            ["roots"] = roots,
            ["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(roots)
        }.ToJsonString());
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(DonkeyJvm, "ok", irDoc.RootElement, root);
        Assert.True(entry.ReferenceCuboidsMatch, entry.ReferenceCompareMessage);
    }

    private static HashSet<string> CollectPartIds(JsonArray roots)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
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

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                return o;
            }

            if (o["children"] is JsonArray kids && FindPartById(kids, id) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void AssertChildPartId(JsonArray children, string id)
    {
        Assert.Contains(children, n =>
            n is JsonObject o && string.Equals((string?)o["id"], id, StringComparison.Ordinal));
    }
}
