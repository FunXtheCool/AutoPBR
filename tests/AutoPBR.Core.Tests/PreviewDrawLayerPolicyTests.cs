using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class PreviewDrawLayerPolicyTests
{
    [Fact]
    public void PreviewDrawBatch_defaults_to_base_layer_policy()
    {
        var batch = new PreviewDrawBatch(0, 12, 0);
        Assert.Equal(PreviewDepthLayerKind.Base, batch.LayerPolicy.Kind);
        Assert.True(batch.LayerPolicy.DepthWrite);
        Assert.Equal(PreviewDrawLayerShadowMode.Draw, batch.LayerPolicy.ShadowMode);
        Assert.Equal(0, batch.LayerPolicy.DepthBiasStep);
    }

    [Fact]
    public void ForKind_cutout_overlay_writes_depth_for_post_occlusion()
    {
        var policy = PreviewDrawLayerPolicy.ForKind(PreviewDepthLayerKind.CutoutOverlay, layerOrdinal: 1);
        Assert.True(policy.DepthWrite);
        Assert.Equal(PreviewDrawLayerShadowMode.Skip, policy.ShadowMode);
        Assert.Equal(2, policy.DepthBiasStep);
    }

    [Fact]
    public void ForKind_cosmetic_overlay_writes_depth_and_skips_shadow()
    {
        var policy = PreviewDrawLayerPolicy.ForKind(PreviewDepthLayerKind.CosmeticOverlay, layerOrdinal: 1);
        Assert.Equal(PreviewDepthLayerKind.CosmeticOverlay, policy.Kind);
        Assert.True(policy.DepthWrite);
        Assert.Equal(PreviewDrawLayerShadowMode.Skip, policy.ShadowMode);
        Assert.Equal(201, policy.DrawOrder);
        Assert.Equal(2, policy.DepthBiasStep);
    }

    [Fact]
    public void ForKind_emissive_overlay_writes_depth_and_skips_shadow()
    {
        var policy = PreviewDrawLayerPolicy.ForKind(PreviewDepthLayerKind.EmissiveOverlay);
        Assert.True(policy.DepthWrite);
        Assert.Equal(PreviewDrawLayerShadowMode.Skip, policy.ShadowMode);
        Assert.Equal(2, policy.DepthBiasStep);
    }

    [Fact]
    public void PreviewDrawBatchOrdering_sorts_by_draw_order_then_material()
    {
        var batches = new List<PreviewDrawBatch>
        {
            new(30, 6, 1) { LayerPolicy = PreviewDrawLayerPolicy.ForKind(PreviewDepthLayerKind.CosmeticOverlay) },
            new(0, 12, 0),
            new(12, 18, 0) { LayerPolicy = PreviewDrawLayerPolicy.ForKind(PreviewDepthLayerKind.CutoutOverlay) },
        };

        PreviewDrawBatchOrdering.Sort(batches);

        Assert.Equal(PreviewDepthLayerKind.Base, batches[0].LayerPolicy.Kind);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, batches[1].LayerPolicy.Kind);
        Assert.Equal(PreviewDepthLayerKind.CosmeticOverlay, batches[2].LayerPolicy.Kind);
    }
}

public sealed class MinecraftModelBakerLayerPolicyTests
{
    [Fact]
    public void TryBake_splits_batches_when_layer_policy_changes_with_same_material()
    {
        var baseTex = "entity/test/base";
        var overlayTex = "entity/test/base_overlay";
        var baseZip = "assets/minecraft/textures/entity/test/base.png";
        var overlayZip = "assets/minecraft/textures/entity/test/base_overlay.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [baseZip] = 0,
            [overlayZip] = 0,
        };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [baseZip] = (64, 64),
            [overlayZip] = (64, 64),
        };

        var model = new MergedJavaBlockModel
        {
            Textures = new Dictionary<string, string>
            {
                ["#layer0"] = baseTex,
                ["#layer1"] = overlayTex,
            },
            Elements = [
                new ModelElement
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    Faces = new Dictionary<string, ModelFace>
                    {
                        ["north"] = new ModelFace { TextureKey = "#layer0" },
                    },
                },
                new ModelElement
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    Faces = new Dictionary<string, ModelFace>
                    {
                        ["north"] = new ModelFace { TextureKey = "#layer1" },
                    },
                },
            ],
        };

        Assert.True(MinecraftModelBaker.TryBake(model, "minecraft", pathToIdx, texSizes, out _, out _, out var batches));
        Assert.Equal(2, batches.Count);
        Assert.Equal(PreviewDepthLayerKind.Base, batches[0].LayerPolicy.Kind);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, batches[1].LayerPolicy.Kind);
        Assert.Equal(0, batches[0].MaterialIndex);
        Assert.Equal(0, batches[1].MaterialIndex);
    }

    [Fact]
    public void TryBake_explicit_element_layer_kind_overrides_texture_heuristic()
    {
        var tex = "entity/test/plain";
        var texZip = "assets/minecraft/textures/entity/test/plain.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [texZip] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [texZip] = (64, 64) };
        var model = new MergedJavaBlockModel
        {
            Textures = new Dictionary<string, string> { ["#layer0"] = tex },
            Elements = [
                new ModelElement
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    DepthLayerKind = PreviewDepthLayerKind.EmissiveOverlay,
                    Faces = new Dictionary<string, ModelFace>
                    {
                        ["north"] = new ModelFace { TextureKey = "#layer0" },
                    },
                },
            ],
        };

        Assert.True(MinecraftModelBaker.TryBake(model, "minecraft", pathToIdx, texSizes, out _, out _, out var batches));
        Assert.Single(batches);
        Assert.Equal(PreviewDepthLayerKind.EmissiveOverlay, batches[0].LayerPolicy.Kind);
    }
}

public sealed class PreviewDepthLayerClassifierTests
{
    [Fact]
    public void ClassifyIrCuboid_body_overlay_on_illager_second_cuboid()
    {
        const string jvm = "net.minecraft.client.model.monster.illager.IllagerModel";
        using var overlay = JsonDocument.Parse(
            """
            {
              "from": [-4, 0, -3],
              "to": [4, 20, 3],
              "uvOrigin": [0, 38],
              "inflate": 0.5
            }
            """);
        var (kind, ordinal, castsShadow) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
            "body",
            jvm,
            overlay.RootElement,
            cuboidIndexOnPart: 1,
            cuboidCountOnPart: 2);
        Assert.Equal(PreviewDepthLayerKind.Base, kind);
        Assert.True(castsShadow);
        Assert.Equal(1, ordinal);
    }

    [Fact]
    public void ClassifyIrCuboid_arms_disjoint_cuboids_stay_base()
    {
        using var leftSleeve = JsonDocument.Parse("{\"uvOrigin\":[44,22]}");
        using var rightSleeve = JsonDocument.Parse("{\"uvOrigin\":[44,22]}");
        var (leftKind, _, _) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
            "arms",
            "net.minecraft.client.model.npc.VillagerModel",
            leftSleeve.RootElement,
            cuboidIndexOnPart: 0,
            cuboidCountOnPart: 3);
        var (rightKind, _, _) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
            "arms",
            "net.minecraft.client.model.npc.VillagerModel",
            rightSleeve.RootElement,
            cuboidIndexOnPart: 2,
            cuboidCountOnPart: 3);
        Assert.Equal(PreviewDepthLayerKind.Base, leftKind);
        Assert.Equal(PreviewDepthLayerKind.Base, rightKind);
    }

    [Fact]
    public void ClassifyIrCuboid_hat_is_cutout_overlay()
    {
        using var hat = JsonDocument.Parse("{\"inflate\": 0.45}");
        var (kind, _, _) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
            "hat",
            "net.minecraft.client.model.monster.illager.IllagerModel",
            hat.RootElement,
            0,
            1);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, kind);
    }
}

public sealed class PreviewCameraDepthRangeTests
{
    [Fact]
    public void ForSubjectBounds_tightens_ratio_vs_defaults()
    {
        var min = new Vector3(-0.5f, 0f, -0.5f);
        var max = new Vector3(0.5f, 1.8f, 0.5f);
        var (near, far) = PreviewCameraDepthRange.ForSubjectBounds(min, max, orbitDistance: 3.2f);
        Assert.True(near > PreviewCameraDepthRange.DefaultNear * 0.5f);
        Assert.True(far < PreviewCameraDepthRange.DefaultFar);
        Assert.True(far / near < PreviewCameraDepthRange.DefaultFar / PreviewCameraDepthRange.DefaultNear);
    }

    [Fact]
    public void ForOrbitPreview_includes_grid_extent_from_eye()
    {
        var eye = new Vector3(2.07f, 1.23f, 3.67f);
        var subjectMin = new Vector3(-0.5f, 0f, -0.5f);
        var subjectMax = new Vector3(0.5f, 1.8f, 0.5f);
        var (near, far) = PreviewCameraDepthRange.ForOrbitPreview(
            subjectMin,
            subjectMax,
            orbitDistance: 3.2f,
            eye,
            environmentHalfExtent: 14f,
            environmentFloorY: -0.56f);

        Assert.True(far > 16f, $"far={far} should cover grid corners from eye");
        Assert.True(near < far);
        Assert.True(far <= PreviewCameraDepthRange.DefaultFar);
    }
}

public sealed class PreviewDepthLayerHeuristicsTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/villager/profession/farmer.png", PreviewDepthLayerKind.CosmeticOverlay)]
    [InlineData("assets/minecraft/textures/entity/sheep/sheep_overlay.png", PreviewDepthLayerKind.CutoutOverlay)]
    public void TryInferKind_matches_overlay_texture_tokens(string path, PreviewDepthLayerKind expected)
    {
        Assert.True(PreviewDepthLayerHeuristics.TryInferKind(path, out var kind));
        Assert.Equal(expected, kind);
    }
}

public sealed class PreviewDepthLayerResolverTests
{
    [Theory]
    [InlineData("#wind", "wind_mid", PreviewDepthLayerKind.CutoutOverlay, 1)]
    [InlineData("#eyes", "body", PreviewDepthLayerKind.CosmeticOverlay, 0)]
    [InlineData("#emissive", "body", PreviewDepthLayerKind.EmissiveOverlay, 0)]
    [InlineData("#skin", "body", PreviewDepthLayerKind.Base, 0)]
    public void TryInferFromTextureKey_classifies_supplementary_factory_keys(
        string textureKey,
        string partId,
        PreviewDepthLayerKind expectedKind,
        int expectedOrdinal)
    {
        var inferred = PreviewDepthLayerResolver.TryInferFromTextureKey(textureKey, partId, out var kind, out var ordinal);
        if (expectedKind == PreviewDepthLayerKind.Base)
        {
            Assert.False(inferred);
            return;
        }

        Assert.True(inferred);
        Assert.Equal(expectedKind, kind);
        Assert.Equal(expectedOrdinal, ordinal);
    }

    [Fact]
    public void EnrichMergedModel_tags_face_texture_key_before_bake()
    {
        var merged = new MergedJavaBlockModel
        {
            Elements = new List<ModelElement>
            {
                new()
                {
                    From = [0, 0, 0],
                    To = [16, 16, 16],
                    Faces = new Dictionary<string, ModelFace>
                    {
                        ["north"] = new ModelFace { TextureKey = "#wind" },
                    },
                },
            },
            Textures = new Dictionary<string, string> { ["#wind"] = "entity/breeze/breeze_wind" },
        };

        PreviewDepthLayerResolver.EnrichMergedModel(merged);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, merged.Elements[0].DepthLayerKind);
    }

    [Fact]
    public void EnrichMergedModel_tags_coplanar_sibling_on_shared_part_pose()
    {
        var bodyPose = Matrix4x4.CreateTranslation(8f, 12f, 8f);
        var merged = new MergedJavaBlockModel
        {
            Elements = new List<ModelElement>
            {
                new()
                {
                    From = [-4, 0, -3],
                    To = [4, 12, 3],
                    LocalToParent = bodyPose,
                    Faces = new Dictionary<string, ModelFace> { ["north"] = new ModelFace { TextureKey = "#skin" } },
                },
                new()
                {
                    From = [-4, 0, -3],
                    To = [4, 20, 3],
                    LocalToParent = bodyPose,
                    Faces = new Dictionary<string, ModelFace> { ["north"] = new ModelFace { TextureKey = "#skin" } },
                },
            },
            Textures = new Dictionary<string, string> { ["#skin"] = "entity/illager/vindicator" },
        };

        PreviewDepthLayerResolver.EnrichMergedModel(merged);
        Assert.Equal(PreviewDepthLayerKind.Base, merged.Elements[0].DepthLayerKind);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, merged.Elements[1].DepthLayerKind);
    }

    [Fact]
    public void ClassifyIrCuboid_honors_explicit_previewDepthLayer_on_ir()
    {
        using var cuboid = JsonDocument.Parse("{\"previewDepthLayer\":\"cosmeticOverlay\"}");
        var (kind, _, _) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
            "body",
            "net.minecraft.client.model.npc.VillagerModel",
            cuboid.RootElement,
            0,
            1);
        Assert.Equal(PreviewDepthLayerKind.CosmeticOverlay, kind);
    }
}
