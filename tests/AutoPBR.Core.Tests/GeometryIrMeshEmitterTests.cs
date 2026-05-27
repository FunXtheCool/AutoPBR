using System.Numerics;
using System.Text.Json;


namespace AutoPBR.Core.Tests;

public sealed class GeometryIrMeshEmitterTests
{
    private const string CodJvmName = "net.minecraft.client.model.animal.fish.CodModel";

    private const string SalmonJvmName = "net.minecraft.client.model.animal.fish.SalmonModel";

    private static string PackagedGeometryPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "geometry", "26.1.2", fileName);

    private static string DocsGeometryPath(params string[] segments) =>
        Path.Combine([AppContext.BaseDirectory, .. segments]);

    [Fact]
    public void Packaged_Cod_geometry_shard_loads()
    {
        var path = PackagedGeometryPath($"{CodJvmName}.json");
        Assert.True(File.Exists(path), $"Missing packaged shard: {path}");

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        Assert.True(GeometryIrDocumentLoader.TryLoad(profile, CodJvmName, out var root));
        Assert.Equal("ok", root.GetProperty("extractionStatus").GetString());
    }

    [Fact]
    public void Packaged_Salmon_geometry_shard_loads()
    {
        var path = PackagedGeometryPath($"{SalmonJvmName}.json");
        Assert.True(File.Exists(path), $"Missing packaged shard: {path}");

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        Assert.True(GeometryIrDocumentLoader.TryLoad(profile, SalmonJvmName, out var root));
        Assert.Equal("ok", root.GetProperty("extractionStatus").GetString());
    }

    [Fact]
    public void Cod_geometry_shard_rest_poses_match_lifted_ir()
    {
        var path = DocsGeometryPath("docs", "generated", "geometry", "26.1.2", $"{CodJvmName}.json");
        if (!File.Exists(path))
        {
            path = PackagedGeometryPath($"{CodJvmName}.json");
        }

        Assert.True(File.Exists(path), $"Missing Cod geometry shard: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var parts = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        CollectPartTree(doc.RootElement.GetProperty("roots")[0], parts);
        Assert.Equal(7, parts.Count);
        AssertTranslation(parts["body"], 0, 22, 0);
        AssertTranslation(parts["head"], 0, 22, 0);
        AssertTranslation(parts["nose"], 0, 22, -3);
        AssertTranslation(parts["right_fin"], -1, 23, 0);
        AssertRadApprox(parts["right_fin"].GetProperty("pose").GetProperty("rotationEulerRad")[2].GetDouble(), -Math.PI / 4, 1e-4);
        AssertTranslation(parts["left_fin"], 1, 23, 0);
        AssertRadApprox(parts["left_fin"].GetProperty("pose").GetProperty("rotationEulerRad")[2].GetDouble(), Math.PI / 4, 1e-4);
        AssertTranslation(parts["tail_fin"], 0, 22, 7);
        AssertTranslation(parts["top_fin"], 0, 20, 0);
        AssertUvOrigin(parts["top_fin"], 20, -6);
    }

    [Fact]
    public void Cod_ir_fidelity_emit_matches_shard_cuboid_count_and_fin_extents()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var p = CleanRoomEntityModelRuntime.BabyProfile.Adult;
        const string texRef = "entity/fish/cod";

        var mesh = CleanRoomEntityModelRuntime.TryBuildCodGeometryIrMeshForTests(texRef, profile, p, tailSway: 0f, out var failure);
        Assert.Null(failure);
        Assert.NotNull(mesh);
        Assert.Equal(7, mesh.Elements.Count);

        Assert.True(HasElementWithLocalExtents(mesh, x0: -2f, y0: 0f, z0: -1f, x1: 0f, y1: 0f, z1: 1f, tol: 1e-3f));
        Assert.True(HasElementWithLocalExtents(mesh, x0: -1f, y0: -2f, z0: 0f, x1: 1f, y1: 2f, z1: 7f, tol: 1e-3f));
    }

    [Fact]
    public void Cod_viewport_emit_thickens_zero_extent_axes_from_ir()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var p = CleanRoomEntityModelRuntime.BabyProfile.Adult;
        const string texRef = "entity/fish/cod";

        var parity = CleanRoomEntityModelRuntime.TryBuildCodGeometryIrMeshForTests(texRef, profile, p, tailSway: 0f, out _);
        var viewport = CleanRoomEntityModelRuntime.BuildAquatic(texRef, profile, isBaby: false, tailSway: 0f);
        Assert.NotNull(parity);
        Assert.Equal(parity.Elements.Count, viewport.Elements.Count);
        Assert.True(HasElementWithLocalExtents(viewport, x0: -2f, y0: -0.08f, z0: -1f, x1: 0f, y1: 0.08f, z1: 1f, tol: 1e-3f));
    }

    [Fact]
    public void Salmon_ir_fidelity_emit_matches_shard_cuboid_count()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var p = CleanRoomEntityModelRuntime.BabyProfile.Adult;
        const string texRef = "entity/fish/salmon";

        var mesh = CleanRoomEntityModelRuntime.TryBuildSalmonGeometryIrMeshForTests(texRef, profile, p, tailSway: 0f, out var failure);
        Assert.Null(failure);
        Assert.NotNull(mesh);
        Assert.Equal(8, mesh.Elements.Count);
    }

    [Fact]
    public void BuildAquatic_emits_geometry_ir_cod_viewport_mesh()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var aquatic = CleanRoomEntityModelRuntime.BuildAquatic(
            "entity/_preview/aquatic_fallback",
            profile,
            isBaby: false,
            tailSway: 0f);
        Assert.Equal(7, aquatic.Elements.Count);
        Assert.True(HasElementWithLocalExtents(
            aquatic, x0: -2f, y0: -0.08f, z0: -1f, x1: 0f, y1: 0.08f, z1: 1f, tol: 1e-3f));
    }

    private static void CollectPartTree(JsonElement part, Dictionary<string, JsonElement> parts)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            idEl.GetString() is { } id &&
            !string.Equals(id, "root", StringComparison.Ordinal))
        {
            parts[id] = part;
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                CollectPartTree(ch, parts);
            }
        }
    }

    private static void AssertMeshesEquivalent(MergedJavaBlockModel a, MergedJavaBlockModel b, float tol)
    {
        Assert.Equal(a.Elements.Count, b.Elements.Count);
        var sortedA = a.Elements.OrderBy(SortKey).ToList();
        var sortedB = b.Elements.OrderBy(SortKey).ToList();
        for (var i = 0; i < sortedA.Count; i++)
        {
            AssertElementNear(sortedA[i], sortedB[i], tol);
        }
    }

    private static string SortKey(ModelElement e) =>
        $"{e.From[0]:F3},{e.From[1]:F3},{e.From[2]:F3},{e.To[0]:F3},{e.To[1]:F3},{e.To[2]:F3}";

    private static void AssertElementNear(ModelElement expected, ModelElement actual, float tol)
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.InRange(actual.From[i], expected.From[i] - tol, expected.From[i] + tol);
            Assert.InRange(actual.To[i], expected.To[i] - tol, expected.To[i] + tol);
        }

        AssertMatrixNear(expected.LocalToParent, actual.LocalToParent, tol);

        foreach (var faceName in new[] { "north", "south", "east", "west", "up", "down" })
        {
            if (!expected.Faces.TryGetValue(faceName, out var ef) || ef.Uv is null)
            {
                continue;
            }

            Assert.True(actual.Faces.TryGetValue(faceName, out var af));
            Assert.NotNull(af.Uv);
            Assert.Equal(ef.TextureKey, af.TextureKey);
            for (var u = 0; u < 4; u++)
            {
                Assert.InRange(af.Uv[u], ef.Uv[u] - tol, ef.Uv[u] + tol);
            }
        }
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tol)
    {
        Assert.InRange(actual.M11, expected.M11 - tol, expected.M11 + tol);
        Assert.InRange(actual.M22, expected.M22 - tol, expected.M22 + tol);
        Assert.InRange(actual.M33, expected.M33 - tol, expected.M33 + tol);
        Assert.InRange(actual.M41, expected.M41 - tol, expected.M41 + tol);
        Assert.InRange(actual.M42, expected.M42 - tol, expected.M42 + tol);
        Assert.InRange(actual.M43, expected.M43 - tol, expected.M43 + tol);
    }

    private static void AssertTranslation(JsonElement part, double x, double y, double z)
    {
        var t = part.GetProperty("pose").GetProperty("translation");
        Assert.Equal(x, t[0].GetDouble(), 8);
        Assert.Equal(y, t[1].GetDouble(), 8);
        Assert.Equal(z, t[2].GetDouble(), 8);
    }

    private static void AssertUvOrigin(JsonElement part, int u, int v)
    {
        var uv = part.GetProperty("cuboids")[0].GetProperty("uvOrigin");
        Assert.Equal(u, uv[0].GetInt32());
        Assert.Equal(v, uv[1].GetInt32());
    }

    private static void AssertRadApprox(double actual, double expected, double tol) =>
        Assert.InRange(actual, expected - tol, expected + tol);

    private static bool HasElementWithLocalExtents(
        MergedJavaBlockModel mesh,
        float x0,
        float y0,
        float z0,
        float x1,
        float y1,
        float z1,
        float tol) =>
        mesh.Elements.Any(e =>
            MathF.Abs(e.From[0] - x0) <= tol &&
            MathF.Abs(e.From[1] - y0) <= tol &&
            MathF.Abs(e.From[2] - z0) <= tol &&
            MathF.Abs(e.To[0] - x1) <= tol &&
            MathF.Abs(e.To[1] - y1) <= tol &&
            MathF.Abs(e.To[2] - z1) <= tol);

    [Fact]
    public void Chicken_packaged_shard_is_v2_and_passes_parity_lift_policy()
    {
        const string jvm = "net.minecraft.client.model.animal.chicken.ChickenModel";
        var path = PackagedGeometryPath($"{jvm}.json");
        if (!File.Exists(path))
        {
            path = DocsGeometryPath("docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        }

        Assert.True(File.Exists(path), $"Missing Chicken geometry shard: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(2, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("ok", doc.RootElement.GetProperty("extractionStatus").GetString());

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(profile, jvm, out var root));
        Assert.Equal(GeometryIrLiftPolicyDecision.Emit, GeometryIrLiftPolicy.EvaluateDocument(root));
        Assert.True(CountCuboidsInPartTree(root) >= 6);
    }

    private static int CountCuboidsInPartTree(JsonElement root)
    {
        var n = 0;
        if (!root.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var part in roots.EnumerateArray())
        {
            n += CountCuboidsInPart(part);
        }

        return n;
    }

    private static int CountCuboidsInPart(JsonElement part)
    {
        var n = 0;
        if (part.TryGetProperty("cuboids", out var cuboids))
        {
            n += cuboids.GetArrayLength();
        }

        if (part.TryGetProperty("children", out var children))
        {
            foreach (var ch in children.EnumerateArray())
            {
                n += CountCuboidsInPart(ch);
            }
        }

        return n;
    }
}
