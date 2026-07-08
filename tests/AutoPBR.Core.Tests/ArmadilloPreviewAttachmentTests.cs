using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed class ArmadilloPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string Jvm = "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel";
    private const string TexturePath = "assets/minecraft/textures/entity/armadillo/armadillo.png";

    [Fact]
    public void Runtime_mesh_preview_world_matches_reference_java_for_body_and_legs()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath,
            Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var opts = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm };

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, opts);
        var bones = EntityGpuShaderDiagnostics.BuildBindPoseBonePalette(bind);
        var snap = EntityGpuShaderDiagnostics.BuildRuntimeSnapshot(
            gpuVerts,
            MinecraftModelBaker.FloatsPerSkinnedVertex,
            partIds,
            bind.Elements.Count,
            boneFillOk: true,
            bonePaletteUploaded: false,
            uploadedGpuSkinning: 0,
            uploadedBoneCount: bind.Elements.Count,
            uploadedLiftY: 0f,
            uploadedBindMesh: 1,
            boneMatrices: bones,
            boneMatrixCount: bones.Length);

        Assert.True(snap.SampleLegBindY > -25f,
            $"leg bind Y still exploded (leg={snap.SampleLegBindY:F3})");
        Assert.True(MathF.Abs(snap.SampleBodyBindY - snap.SampleLegBindY) < 10f,
            $"body-leg bind Y gap too large (body={snap.SampleBodyBindY:F3} leg={snap.SampleLegBindY:F3})");
        Assert.True(snap.SimBodyLegGap < 1.25f,
            $"simBodyLegGap={snap.SimBodyLegGap:F3} (exploded pose)");
    }
}
