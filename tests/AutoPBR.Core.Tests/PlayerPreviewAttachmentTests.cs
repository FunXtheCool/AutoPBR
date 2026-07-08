using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class PlayerPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string PlayerJvm = "net.minecraft.client.model.player.PlayerModel";
    private const string HumanoidJvm = "net.minecraft.client.model.HumanoidModel";
    private const string PlayerWidePath = "assets/minecraft/textures/entity/player/wide/steve.png";
    private const string PlayerSlimPath = "assets/minecraft/textures/entity/player/slim/alex.png";

    [Theory]
    [InlineData(PlayerJvm)]
    [InlineData(HumanoidJvm)]
    public void Player_family_repair_resets_mis_lifted_core_poses(string jvm)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var head = FindPart(repaired, "head");
        var body = FindPart(repaired, "body");
        var rightLeg = FindPart(repaired, "right_leg");
        Assert.NotNull(head);
        Assert.NotNull(body);
        Assert.NotNull(rightLeg);

        Assert.InRange(head!["pose"]!["translation"]![1]!.GetValue<double>(), -0.05, 0.05);
        Assert.InRange(body!["pose"]!["translation"]![1]!.GetValue<double>(), -0.05, 0.05);
        Assert.InRange(rightLeg!["pose"]!["translation"]![1]!.GetValue<double>(), 11.5, 12.5);
        Assert.InRange(rightLeg["pose"]!["translation"]![0]!.GetValue<double>(), -2.1, -1.7);
    }

    [Theory]
    [InlineData(PlayerWidePath, "PlayerWide")]
    [InlineData(PlayerSlimPath, "PlayerSlim")]
    public void Player_runtime_mesh_arms_stay_attached_to_torso(string texturePath, string _)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                texturePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out var provenance,
                applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
            Assert.True(mesh.Elements.Count >= 6, "head/body/arms/legs");

            var repo = GeometryIrTestTierSupport.FindRepoRoot();
            var shardPath = Path.Combine(
                repo,
                "docs",
                "generated",
                "geometry",
                "26.1.2",
                "net.minecraft.client.model.player.PlayerModel.json");
            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PlayerJvm, shard.RootElement);
            var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
                repaired,
                GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PlayerJvm });

            var headCenter = ElementCenter(FindElement(mesh, partIds, "head"));
            var bodyCenter = ElementCenter(FindElement(mesh, partIds, "body"));
            var rightArmCenter = ElementCenter(FindElement(mesh, partIds, "right_arm"));
            Assert.True(
                rightArmCenter.Y < headCenter.Y + 1f,
                $"arm floating above head: armY={rightArmCenter.Y:F2} headY={headCenter.Y:F2}");
            Assert.True(
                MathF.Abs(rightArmCenter.Y - bodyCenter.Y) < 5f,
                $"arm detached from torso: armY={rightArmCenter.Y:F2} bodyY={bodyCenter.Y:F2}");
            AssertTorsoMeetsLegs(mesh, partIds);
        }
    }

    [Theory]
    [InlineData(PlayerWidePath)]
    [InlineData(PlayerSlimPath)]
    public void Player_runtime_mesh_torso_meets_legs_without_gap(string texturePath)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                texturePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: true));

            var repo = GeometryIrTestTierSupport.FindRepoRoot();
            var shardPath = Path.Combine(
                repo,
                "docs",
                "generated",
                "geometry",
                "26.1.2",
                "net.minecraft.client.model.player.PlayerModel.json");
            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PlayerJvm, shard.RootElement);
            var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
                repaired,
                GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PlayerJvm });
            AssertTorsoMeetsLegs(mesh, partIds);
        }
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

    private static ModelElement FindElement(MergedJavaBlockModel mesh, IReadOnlyList<string> partIds, string partId)
    {
        for (var i = 0; i < partIds.Count; i++)
        {
            if (string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                return mesh.Elements[i];
            }
        }

        throw new InvalidOperationException($"missing mesh element for '{partId}'");
    }

    private static Vector3 ElementCenter(ModelElement el)
    {
        TransformWorldCorners(el, out var min, out var max);
        return (min + max) * 0.5f;
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
            if (n is not JsonObject part)
            {
                continue;
            }

            if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
            {
                return part;
            }

            if (part["children"] is JsonArray kids &&
                FindPartObject(kids, partId) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
