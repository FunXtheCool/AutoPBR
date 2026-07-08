using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class ZombiePreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string ZombieJvm = "net.minecraft.client.model.monster.zombie.ZombieModel";
    private const string ZombiePath = "assets/minecraft/textures/entity/zombie/zombie.png";
    private const string HuskPath = "assets/minecraft/textures/entity/zombie/husk.png";

    [Fact]
    public void ZombieModel_reference_repair_applies_canonical_humanoid_uv_origins()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{ZombieJvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(ZombieJvm, shard.RootElement);
        AssertUv(repaired, "body", 16, 16);
        AssertUv(repaired, "left_arm", 40, 16);
        AssertUv(repaired, "right_arm", 40, 16);
        AssertUv(repaired, "left_leg", 0, 16);
        AssertUv(repaired, "right_leg", 0, 16);
    }

    [Theory]
    [InlineData(ZombiePath)]
    [InlineData(HuskPath)]
    public void Zombie_family_runtime_mesh_torso_meets_legs_without_gap(string texturePath)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{ZombieJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(ZombieJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = ZombieJvm });
        AssertTorsoMeetsLegs(mesh, partIds);
    }

    private static void AssertUv(JsonElement geometryRoot, string partId, int u, int v)
    {
        var part = FindPart(geometryRoot, partId);
        Assert.NotNull(part);
        var uv = part!["cuboids"]![0]!["uvOrigin"]!.AsArray();
        Assert.Equal(u, uv[0]!.GetValue<int>());
        Assert.Equal(v, uv[1]!.GetValue<int>());
    }

    private static void AssertTorsoMeetsLegs(MergedJavaBlockModel mesh, IReadOnlyList<string> partIds)
    {
        TransformWorldCorners(FindElement(mesh, partIds, "body"), out var bodyMin, out var bodyMax);
        TransformWorldCorners(FindElement(mesh, partIds, "left_leg"), out var legMin, out var legMax);
        TransformWorldCorners(FindElement(mesh, partIds, "right_leg"), out var rLegMin, out var rLegMax);
        var bodyWaistY = MathF.Min(bodyMin.Y, bodyMax.Y);
        var legHipY = MathF.Max(MathF.Max(legMin.Y, legMax.Y), MathF.Max(rLegMin.Y, rLegMax.Y));
        var gap = legHipY - bodyWaistY;
        Assert.True(
            MathF.Abs(gap) < 0.75f,
            $"torso-leg gap: bodyWaistY={bodyWaistY:F2} legHipY={legHipY:F2} gap={gap:F2}");
    }

    private static ModelElement FindElement(MergedJavaBlockModel mesh, IReadOnlyList<string> partId, string partIdName)
    {
        for (var i = 0; i < partId.Count; i++)
        {
            if (string.Equals(partId[i], partIdName, StringComparison.Ordinal))
            {
                return mesh.Elements[i];
            }
        }

        throw new InvalidOperationException($"missing mesh element for '{partIdName}'");
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        var m = el.LocalToParent;
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var fx = el.From[0];
        var fy = el.From[1];
        var fz = el.From[2];
        var tx = el.To[0];
        var ty = el.To[1];
        var tz = el.To[2];
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
            (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }

    private static JsonObject? FindPart(JsonElement geometryRoot, string partId)
    {
        var node = JsonNode.Parse(geometryRoot.GetRawText());
        if (node?["roots"]?[0]?["children"] is not JsonArray kids)
        {
            return null;
        }

        return FindPartObject(kids, partId);
    }

    private static JsonObject? FindPartObject(JsonArray parts, string partId)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], partId, StringComparison.Ordinal))
            {
                return o;
            }

            if (o["children"] is JsonArray kids && FindPartObject(kids, partId) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
