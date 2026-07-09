using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png")]
    public void Boat_family_static_mesh_does_not_fold_living_entity_renderer_basis(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.False(mesh.UsesLivingEntityRendererColumnYFlip, path);

        var scratch = new List<Matrix4x4>();
        Assert.True(runtime.TryFillBoneMatricesFast(path, Profile26, 0f, 0f, scratch, out var boneCount), path);
        Assert.Equal(mesh.Elements.Count, boneCount);
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(
                MatricesClose(scratch[i], mesh.Elements[i].LocalToParent, 1e-4f),
                $"{path}: bone {i} must match bind LocalToParent (no LER double-apply)");
        }
    }

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps) =>
        MathF.Abs(a.M11 - b.M11) <= eps && MathF.Abs(a.M22 - b.M22) <= eps && MathF.Abs(a.M33 - b.M33) <= eps &&
        MathF.Abs(a.M44 - b.M44) <= eps && MathF.Abs(a.M41 - b.M41) <= eps && MathF.Abs(a.M42 - b.M42) <= eps &&
        MathF.Abs(a.M43 - b.M43) <= eps;

    private static ModelElement FindBoatHullBottomSlab(MergedJavaBlockModel model)
    {
        foreach (var el in model.Elements)
        {
            var lx = MathF.Abs(el.To[0] - el.From[0]);
            var ly = MathF.Abs(el.To[1] - el.From[1]);
            var lz = MathF.Abs(el.To[2] - el.From[2]);
            if (lx is >= 27f and <= 29f &&
                ly is >= 15f and <= 17f &&
                lz is >= 2.5f and <= 3.5f)
            {
                return el;
            }
        }

        throw new InvalidOperationException("boat hull bottom slab (28×16×3 local) not found");
    }

    [Fact]
    public void BoatOak_resolved_geometry_root_is_full_shard_document()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var rule = EntityTextureParityCatalog.ResolveRule(path, "oak");
        Assert.NotNull(rule);
        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26,
                rule!,
                path,
                "oak",
                isBaby: false,
                out var jvm,
                out var geometryRoot),
            path);
        Assert.Equal("net.minecraft.client.model.object.boat.BoatModel", jvm);
        Assert.True(geometryRoot.TryGetProperty("roots", out var roots));
        Assert.Equal(JsonValueKind.Array, roots.ValueKind);
    }
}
