using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void ArmorStand_resolves_standard_living_entity_renderer_basis_despite_object_jvm_package()
    {
        const string path = "assets/minecraft/textures/entity/armorstand/armorstand.png";
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(jvm, "armorstand", path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, basis);
        Assert.True(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, "armorstand"));
        Assert.True(EntityModelRuntime.UsesLivingEntityRendererDespiteObjectPackage(jvm, path));
    }

    [Fact]
    public void ArmorStand_geometry_ir_mesh_folds_living_entity_renderer_and_orients_upright()
    {
        const string path = "assets/minecraft/textures/entity/armorstand/armorstand.png";
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(mesh.UsesLivingEntityRendererColumnYFlip, path);

        var stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, path, stem, isBaby: false, out var resolvedJvm, out var geometryRoot));
        Assert.Equal(jvm, resolvedJvm);
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                OfficialJvmName = jvm,
                AtlasWidth = 64,
                AtlasHeight = 64,
            });

        float headY = 0f;
        var headCount = 0;
        float plateY = 0f;
        var plateCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var centroidY = MeasureElementPreviewCentroidY(mesh.Elements[i]);
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("stick", StringComparison.OrdinalIgnoreCase))
            {
                headY += centroidY;
                headCount++;
            }

            if (partId.Contains("base_plate", StringComparison.OrdinalIgnoreCase))
            {
                plateY += centroidY;
                plateCount++;
            }
        }

        Assert.True(headCount > 0 && plateCount > 0);
        headY /= headCount;
        plateY /= plateCount;
        Assert.True(
            headY > plateY,
            $"armor stand should be upright: headY={headY:F3} plateY={plateY:F3}");

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            if (!string.Equals(partId, "head", StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[i];
            var extentX = MathF.Abs(el.To[0] - el.From[0]);
            var extentY = MathF.Abs(el.To[1] - el.From[1]);
            Assert.InRange(extentX, 1.5f, 2.5f);
            Assert.InRange(extentY, 6.5f, 7.5f);
        }
    }

}
