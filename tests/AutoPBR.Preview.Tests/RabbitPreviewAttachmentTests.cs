using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Preview.Tests;

public sealed class RabbitPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string AdultJvm = "net.minecraft.client.model.animal.rabbit.AdultRabbitModel";
    private const string BabyJvm = "net.minecraft.client.model.animal.rabbit.BabyRabbitModel";
    private const string AdultTexturePath = "assets/minecraft/textures/entity/rabbit/rabbit_caerbannog.png";
    private const string BabyTexturePath = "assets/minecraft/textures/entity/rabbit/rabbit_caerbannog_baby.png";

    private readonly ITestOutputHelper _output;

    public RabbitPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Adult_rabbit_repair_keeps_frontlegs_and_backlegs_group_hierarchy()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(AdultJvm, raw);

        Assert.True(DirectChildOf(repaired, "left_front_leg", "frontlegs"));
        Assert.True(DirectChildOf(repaired, "right_front_leg", "frontlegs"));
        Assert.True(DirectChildOf(repaired, "right_haunch", "right_hind_leg"));
        Assert.False(DirectChildOf(repaired, "left_front_leg", "body"));
        Assert.False(DirectChildOf(repaired, "right_hind_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Adult_rabbit_runtime_mesh_legs_attach_to_body_shell_in_preview_space()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{AdultJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            AdultTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(AdultJvm, provenance.Detail, StringComparison.Ordinal);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(AdultJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = AdultJvm });

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [AdultTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [AdultTexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var (bodyMaxY, legMinY, hullGap) = MeasureBodyLegPreviewHullGap(gpuVerts, partIds);
        _output.WriteLine($"adult bodyMaxY={bodyMaxY:F4} legMinY={legMinY:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.35f, $"adult rabbit legs detached from body (gap={hullGap:F3})");
    }

    [Fact]
    public void Baby_rabbit_runtime_mesh_legs_attach_to_body_shell_in_preview_space()
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
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(BabyJvm, provenance.Detail, StringComparison.Ordinal);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(BabyJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = BabyJvm });

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [BabyTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [BabyTexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var (bodyMaxY, legMinY, hullGap) = MeasureBodyLegPreviewHullGap(gpuVerts, partIds);
        _output.WriteLine($"baby bodyMaxY={bodyMaxY:F4} legMinY={legMinY:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.35f, $"baby rabbit legs detached from body (gap={hullGap:F3})");
    }

    private static (float BodyMaxY, float LegMinY, float HullGap) MeasureBodyLegPreviewHullGap(
        ReadOnlySpan<float> gpuVerts,
        List<string> partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var bodyMaxY = float.NegativeInfinity;
        var legMinY = float.PositiveInfinity;
        for (var i = 0; i + stride - 1 < gpuVerts.Length; i += stride)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(gpuVerts[i + 12]);
            if (bi < 0 || bi >= partIds.Count)
            {
                continue;
            }

            var preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(
                new Vector3(gpuVerts[i], gpuVerts[i + 1], gpuVerts[i + 2]));
            var id = partIds[bi];
            if (id.Contains("body", StringComparison.Ordinal) && !id.Contains("inner", StringComparison.Ordinal))
            {
                bodyMaxY = MathF.Max(bodyMaxY, preview.Y);
            }
            else if (id.Contains("leg", StringComparison.Ordinal) || id.Contains("haunch", StringComparison.Ordinal))
            {
                legMinY = MathF.Min(legMinY, preview.Y);
            }
        }

        return (bodyMaxY, legMinY, legMinY - bodyMaxY);
    }

    private static bool DirectChildOf(JsonElement geometryRoot, string childId, string parentId)
    {
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children))
            {
                continue;
            }

            if (TryFindPart(children, parentId, out var parent) &&
                parent.TryGetProperty("children", out var parentKids) &&
                parentKids.EnumerateArray().Any(ch =>
                    ch.TryGetProperty("id", out var id) &&
                    string.Equals(id.GetString(), childId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPart(JsonElement parts, string id, out JsonElement found)
    {
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), id, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part.TryGetProperty("children", out var kids) && TryFindPart(kids, id, out found))
            {
                return true;
            }
        }

        found = default;
        return false;
    }
}
