using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Attached parts (horns/ears) and rotated torsos must match reference_java full affines — not just part origins.
/// </summary>
public sealed class AttachedPartWorldMatrixParityTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Theory]
    [InlineData("net.minecraft.client.model.animal.cow.ColdCowModel", 64, 64, "right_horn", "left_horn")]
    [InlineData("net.minecraft.client.model.animal.polarbear.PolarBearModel", 128, 64, "body")]
    [InlineData("net.minecraft.client.model.animal.goat.GoatModel", 64, 64, "left_horn", "right_horn")]
    [InlineData("net.minecraft.client.model.animal.goat.BabyGoatModel", 64, 64, "left_horn", "right_horn")]
    public void Catalog_mesh_part_affine_matches_reference_java_model_space_before_ler(
        string jvm,
        int atlasW,
        int atlasH,
        params string[] partIds)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var refMatrices = ReferencePartWorldMatrixIndex.Build(reference.RootElement, jvm);
        var shardPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var mesh = EntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "entity/test",
            jvm,
            atlasW,
            atlasH,
            repaired,
            out var err);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var emitOptions = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm };
        var elementPartIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, emitOptions);

        foreach (var partId in partIds)
        {
            Assert.True(refMatrices.TryGetValue(partId, out var refWorld), $"{jvm}: missing reference matrix for '{partId}'");
            Matrix4x4? meshWorld = null;
            for (var i = 0; i < mesh!.Elements.Count; i++)
            {
                if (string.Equals(elementPartIds[i], partId, StringComparison.Ordinal))
                {
                    meshWorld = mesh.Elements[i].LocalToParent;
                    break;
                }
            }

            Assert.NotNull(meshWorld);
            AssertMatrixNear(refWorld, meshWorld.Value, 0.02f, partId);
            AssertProbePointsNear(refWorld, meshWorld.Value, 0.02f, partId);
        }
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.dolphin.DolphinModel", 64, 64, "left_fin", "right_fin", "back_fin")]
    public void Dolphin_catalog_mesh_part_affine_matches_reference_java_model_space(
        string jvm,
        int atlasW,
        int atlasH,
        params string[] partIds)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var refMatrices = ReferencePartWorldMatrixIndex.Build(reference.RootElement, jvm);
        var shardPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var mesh = EntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "assets/minecraft/textures/entity/dolphin/dolphin.png",
            jvm,
            atlasW,
            atlasH,
            repaired,
            out var err);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var emitOptions = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm };
        var elementPartIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, emitOptions);

        foreach (var partId in partIds)
        {
            Assert.True(refMatrices.TryGetValue(partId, out var refWorld), $"missing reference_java matrix for '{partId}'");
            Matrix4x4? meshWorld = null;
            for (var i = 0; i < mesh!.Elements.Count; i++)
            {
                if (string.Equals(elementPartIds[i], partId, StringComparison.Ordinal))
                {
                    meshWorld = mesh.Elements[i].LocalToParent;
                    break;
                }
            }

            Assert.NotNull(meshWorld);
            AssertMatrixNear(refWorld, meshWorld.Value, 0.08f, partId);
            AssertProbePointsNear(refWorld, meshWorld.Value, 0.08f, partId);
        }
    }


    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold.png", "net.minecraft.client.model.animal.cow.ColdCowModel", 64, 64, "right_horn")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", "net.minecraft.client.model.animal.polarbear.PolarBearModel", 128, 64, "body")]
    public void Explore_catalog_static_mesh_attached_part_preview_points_match_reference(
        string texturePath,
        string jvm,
        int atlasW,
        int atlasH,
        string partId)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var refMatrices = ReferencePartWorldMatrixIndex.Build(reference.RootElement, jvm);
        Assert.True(refMatrices.TryGetValue(partId, out var refModelWorld));

        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm });
        var ler = EntityModelRuntime.LivingEntityRendererPreviewRootScale;

        Matrix4x4? meshWorld = null;
        for (var i = 0; i < mesh!.Elements.Count; i++)
        {
            if (string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                meshWorld = mesh.Elements[i].LocalToParent;
                break;
            }
        }

        Assert.NotNull(meshWorld);
        foreach (var probeLocal in new[] { Vector3.Zero, new Vector3(0f, -3f, 0.5f), Vector3.UnitX })
        {
            var refPreview = Vector3.Transform(Vector3.Transform(probeLocal, refModelWorld), ler);
            var meshPreview = Vector3.Transform(probeLocal, meshWorld.Value);
            Assert.True(Vector3.Distance(refPreview, meshPreview) <= 0.05f,
                $"{texturePath} {partId} probe={probeLocal}: ref={refPreview} mesh={meshPreview}");
        }
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tol, string label)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var e = expected[row, col];
                var a = actual[row, col];
                Assert.True(MathF.Abs(e - a) <= tol, $"{label} M{row + 1}{col + 1}: expected={e:R} actual={a:R}");
            }
        }
    }

    private static void AssertProbePointsNear(Matrix4x4 expected, Matrix4x4 actual, float tol, string label)
    {
        foreach (var p in new[]
                 {
                     Vector3.Zero,
                     new Vector3(1f, -2f, 0.5f),
                     new Vector3(-0.5f, 0f, 1f),
                 })
        {
            var e = Vector3.Transform(p, expected);
            var a = Vector3.Transform(p, actual);
            Assert.True(Vector3.Distance(e, a) <= tol,
                $"{label} probe {p}: expected={e} actual={a}");
        }
    }

    /// <summary>Reference tree walk using production <see cref="EntityModelRuntime.TryComposePartPosePublic"/>.</summary>
    private static class ReferencePartWorldMatrixIndex
    {
        public static Dictionary<string, Matrix4x4> Build(JsonElement referenceRoot, string? officialJvmName = null)
        {
            var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            if (!referenceRoot.TryGetProperty("roots", out var roots))
            {
                return map;
            }

            foreach (var root in roots.EnumerateArray())
            {
                Visit(root, Matrix4x4.Identity, map, officialJvmName);
            }

            return map;
        }

        private static void Visit(
            JsonElement part,
            Matrix4x4 parentWorld,
            Dictionary<string, Matrix4x4> sink,
            string? officialJvmName)
        {
            var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var world = parentWorld;
            if (part.TryGetProperty("pose", out var poseEl))
            {
                if (!EntityModelRuntime.TryComposePartPosePublic(poseEl, parentWorld, out var worldTexel, partId))
                {
                    world = parentWorld;
                }
                else
                {
                    world = worldTexel;
                }
            }

            if (!string.IsNullOrEmpty(partId))
            {
                sink[partId] = world;
            }

            if (part.TryGetProperty("children", out var children))
            {
                foreach (var ch in children.EnumerateArray())
                {
                    Visit(ch, world, sink, officialJvmName);
                }
            }
        }
    }
}
