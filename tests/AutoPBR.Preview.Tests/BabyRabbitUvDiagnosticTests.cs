using System.Collections.Generic;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Preview.Entities;
using AutoPBR.Preview.GeometryIr;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Preview.Tests;

[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class BabyRabbitUvDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    private const string BabyTexturePath = "assets/minecraft/textures/entity/rabbit/rabbit_caerbannog_baby.png";
    private const string BabyJvm = "net.minecraft.client.model.animal.rabbit.BabyRabbitModel";

    [Fact]
    public void Baby_rabbit_head_keeps_logical_32x32_bake_atlas_after_depth_layer_enrich()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{BabyJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(BabyJvm, shard.RootElement);
        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/rabbit/rabbit_caerbannog_baby",
            Profile26,
            BabyJvm,
            32,
            32,
            out _,
            repaired)!;

        var head = mesh.Elements.First(e =>
            MathF.Abs(e.From[0] + 2.5f) < 0.01f && MathF.Abs(e.From[1] + 3f) < 0.01f);
        Assert.Equal(32, head.BakeAtlasWidth);
        Assert.Equal(32, head.BakeAtlasHeight);

        PreviewDepthLayerResolver.EnrichMergedModel(mesh, BabyJvm);

        head = mesh.Elements.First(e =>
            MathF.Abs(e.From[0] + 2.5f) < 0.01f && MathF.Abs(e.From[1] + 3f) < 0.01f);
        Assert.Equal(32, head.BakeAtlasWidth);
        Assert.Equal(32, head.BakeAtlasHeight);
    }

    [Fact]
    public void Baby_rabbit_head_north_face_samples_logical_32x32_atlas_on_64px_texture()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{BabyJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            BabyTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var merged,
            out var provenance));

        var bakeSize = EntityGeometryIrTextureAtlas.ResolveForBake(
            BabyTexturePath,
            physicalWidth: 64,
            physicalHeight: 64,
            provenance,
            Profile26);
        Assert.Equal(32, bakeSize.Width);
        Assert.Equal(32, bakeSize.Height);

        var head = merged.Elements.First(e =>
            MathF.Abs(e.From[0] + 2.5f) < 0.01f && MathF.Abs(e.From[1] + 3f) < 0.01f);
        Assert.Equal(32, head.BakeAtlasWidth);
        Assert.True(head.Faces.TryGetValue("north", out var northFace));
        Assert.NotNull(northFace);
        Assert.NotNull(northFace.Uv);
        Assert.Equal(4f, northFace.Uv[0], 0.01f);
        Assert.Equal(4f, northFace.Uv[1], 0.01f);
        Assert.Equal(9f, northFace.Uv[2], 0.01f);
        Assert.Equal(8f, northFace.Uv[3], 0.01f);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [BabyTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [BabyTexturePath] = (bakeSize.Width, bakeSize.Height),
        };
        Assert.True(MinecraftModelBaker.TryBake(
            merged,
            "minecraft",
            pathToIdx,
            texSizes,
            out var verts,
            out _,
            out _));

        var uvFp = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMeshUvFingerprint(
            verts,
            MinecraftModelBaker.FloatsPerVertex);
        Assert.Equal(2745455757088240869UL, uvFp);
    }
}
