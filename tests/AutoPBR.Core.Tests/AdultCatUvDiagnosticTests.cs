using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Adult feline head uses texCrop <c>addBox</c> quads (<c>#nose</c>, <c>#ear1</c>, <c>#ear2</c>); UV footprint must follow box dims.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class AdultCatUvDiagnosticTests
{
    private const string AdultCatJvm = "net.minecraft.client.model.animal.feline.AdultCatModel";
    private const string TexturePath = "assets/minecraft/textures/entity/cat/cat_calico.png";

    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Part_tree_repair_rewrites_stale_texCrop_uvSpan_anchor_echo_for_adult_cat()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultCatJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var stale = JsonNode.Parse(shard.RootElement.GetRawText())!.AsObject();
        var nose = FindMutableCuboidByTextureKey(stale, "head", "#nose");
        Assert.NotNull(nose);
        nose!["uvSpan"] = new JsonArray { 0, 24 };

        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(
            AdultCatJvm,
            JsonDocument.Parse(stale.ToJsonString()).RootElement);
        var head = FindPart(repaired, "head");
        Assert.True(head.HasValue);
        var fixedNose = FindCuboidByTextureKey(head.Value, "#nose");
        Assert.True(fixedNose.HasValue);
        Assert.Equal(3, fixedNose!.Value.GetProperty("uvSpan")[0].GetInt32());
        Assert.Equal(2, fixedNose.Value.GetProperty("uvSpan")[1].GetInt32());
        Assert.Equal(2, fixedNose.Value.GetProperty("uvSpan")[2].GetInt32());
    }

    [Fact]
    public void Adult_cat_rebake_uses_shard_64x32_atlas_not_manifest_placeholder()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out _, out var provenance));

        var size = EntityGeometryIrTextureAtlas.ResolveForBake(TexturePath, 64, 64, provenance, Profile26);
        Assert.Equal((64, 32), (size.Width, size.Height));
    }

    [Fact]
    public void Adult_cat_rebaked_uv_fingerprint_matches_logical_64x32_atlas()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var logical = EntityGeometryIrTextureAtlas.ResolveForBake(TexturePath, 64, 64, provenance, Profile26);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [ordered[0]] = logical,
        };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var baked, out _, out _));

        var fp = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMeshUvFingerprint(
            baked, MinecraftModelBaker.FloatsPerVertex);
        Assert.Equal(10285574214237588581UL, fp);
    }

    [Fact]
    public void Adult_cat_ok_shard_uv_fits_committed_atlas()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultCatJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var uv = GeometryIrUvAtlasQuality.Evaluate(shard.RootElement);
        Assert.True(uv.UvWithinAtlasMatch, uv.Message);
    }

    [Fact]
    public void Adult_cat_static_mesh_nose_texCrop_north_face_uses_java_unfold_slot()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, 0f, 0f, out var mesh, out var provenance));
        Assert.Contains(AdultCatJvm, provenance.Detail ?? "", StringComparison.Ordinal);

        var expectedNorth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.North, 0, 24, 3, 2, 2);
        var noseNorth = mesh.Elements
            .SelectMany(el => el.Faces.Where(f => f.Key == "north").Select(f => f.Value.Uv))
            .FirstOrDefault(uv => uv is { Length: 4 } &&
                                  MathF.Abs(uv[0] - expectedNorth[0]) < 0.02f &&
                                  MathF.Abs(uv[1] - expectedNorth[1]) < 0.02f);
        Assert.NotNull(noseNorth);
        Assert.Equal(expectedNorth, noseNorth);
    }

    [Fact]
    public void Adult_cat_ok_shard_head_texCrop_uvSpan_matches_geometry_not_anchor()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultCatJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var head = FindPart(shard.RootElement, "head");
        Assert.True(head.HasValue);

        var nose = FindCuboidByTextureKey(head.Value, "#nose");
        Assert.True(nose.HasValue);
        Assert.Equal(3, nose.Value.GetProperty("uvSpan")[0].GetInt32());
        Assert.Equal(2, nose.Value.GetProperty("uvSpan")[1].GetInt32());
        Assert.Equal(2, nose.Value.GetProperty("uvSpan")[2].GetInt32());
    }

    private static System.Text.Json.JsonElement? FindPart(System.Text.Json.JsonElement geometryRoot, string partId)
    {
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.GetArrayLength() == 0)
        {
            return null;
        }

        return FindPartInChildren(roots[0].GetProperty("children"), partId);
    }

    private static System.Text.Json.JsonElement? FindPartInChildren(System.Text.Json.JsonElement children, string partId)
    {
        foreach (var child in children.EnumerateArray())
        {
            if (child.TryGetProperty("id", out var id) &&
                string.Equals(id.GetString(), partId, StringComparison.Ordinal))
            {
                return child;
            }

            if (child.TryGetProperty("children", out var nested))
            {
                var found = FindPartInChildren(nested, partId);
                if (found.HasValue)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static System.Text.Json.JsonElement? FindCuboidByTextureKey(System.Text.Json.JsonElement part, string textureKey)
    {
        if (!part.TryGetProperty("cuboids", out var cuboids))
        {
            return null;
        }

        foreach (var cuboid in cuboids.EnumerateArray())
        {
            if (cuboid.TryGetProperty("textureKey", out var tk) &&
                string.Equals(tk.GetString(), textureKey, StringComparison.OrdinalIgnoreCase))
            {
                return cuboid;
            }
        }

        return null;
    }

    private static JsonObject? FindMutableCuboidByTextureKey(JsonObject geometryDoc, string partId, string textureKey)
    {
        if (geometryDoc["roots"] is not JsonArray roots || roots.Count == 0 || roots[0] is not JsonObject root)
        {
            return null;
        }

        return FindMutableCuboidByTextureKeyRecursive(root, partId, textureKey);
    }

    private static JsonObject? FindMutableCuboidByTextureKeyRecursive(JsonObject part, string partId, string textureKey)
    {
        if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal) &&
            part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboid in cuboids)
            {
                if (cuboid is JsonObject co &&
                    string.Equals((string?)co["textureKey"], textureKey, StringComparison.OrdinalIgnoreCase))
                {
                    return co;
                }
            }
        }

        if (part["children"] is not JsonArray children)
        {
            return null;
        }

        foreach (var child in children)
        {
            if (child is JsonObject childObj)
            {
                var found = FindMutableCuboidByTextureKeyRecursive(childObj, partId, textureKey);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
