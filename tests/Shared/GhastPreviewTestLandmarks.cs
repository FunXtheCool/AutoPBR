using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Tests.TestSupport;

/// <summary>
/// Shared ghast-family IR landmark gates (T1). Rejects CleanRoom hand-build placement and unreoriented +Y tentacles.
/// </summary>
internal static class GhastPreviewTestLandmarks
{
    public const string MonsterJvm = "net.minecraft.client.model.monster.ghast.GhastModel";
    public const string HappyJvm = "net.minecraft.client.model.animal.ghast.HappyGhastModel";
    public const string MonsterTexturePath = "assets/minecraft/textures/entity/ghast/ghast.png";
    public const string HappyTexturePath = "assets/minecraft/textures/entity/ghast/happy_ghast.png";

    public static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    /// <summary>Committed T1 shard must exist with <c>extractionStatus: ok</c> — do not silently skip.</summary>
    public static string RequireOkCommittedShardPath(string officialJvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(
            repo,
            "docs",
            "generated",
            "geometry",
            "26.1.2",
            $"{officialJvmName}.json");
        Assert.True(File.Exists(shardPath), $"Committed geometry IR shard missing: {shardPath}");
        Assert.True(
            GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status),
            $"Could not read extractionStatus from {shardPath}");
        Assert.Equal("ok", status);
        return shardPath;
    }

    public static void AssertRuntimeGeometryIrDriver(PreviewMeshProvenance provenance, string expectedJvmFragment)
    {
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(expectedJvmFragment, provenance.Detail ?? "", StringComparison.Ordinal);
    }

    public static void AssertNotCleanRoomBodyLocalCube(MergedJavaBlockModel bind)
    {
        Assert.True(bind.Elements.Count >= 10, "expected body + 9 tentacles");
        var body = bind.Elements[0];
        var bodyLocalMinY = MathF.Min(body.From[1], body.To[1]);
        var bodyLocalMaxY = MathF.Max(body.From[1], body.To[1]);
        // CleanRoom BuildGhast body local Y is [9.6, 25.6] at identity root; IR body is centered 16³.
        Assert.InRange(bodyLocalMinY, -8.1f, 8.1f);
        Assert.InRange(bodyLocalMaxY, -8.1f, 8.1f);
        Assert.True(bodyLocalMaxY - bodyLocalMinY >= 15.5f, "IR ghast body should span ~16 texels in Y");
    }

    public static void AssertMonsterGhastReferenceWorldLandmarks(MergedJavaBlockModel bind)
    {
        AssertWorldAabbClose(
            bind.Elements[0],
            new Vector3(-8f, -74.456f, -8f),
            new Vector3(8f, -58.456f, 8f),
            0.08f);
        // Tentacle 0 at bind-pose animateTentacles xRot (~0.4 rad at age 0), Skip-LER + −Y reorient.
        AssertWorldAabbClose(
            bind.Elements[1],
            new Vector3(-4.75f, -67.213905f, -9.036407f),
            new Vector3(-2.75f, -59.06658f, -4.078939f),
            0.08f);
    }

    public static void AssertGhastIrAssemblyLandmarks(
        MergedJavaBlockModel bind,
        IReadOnlyList<string> partIds,
        float expectedBodyMaxY,
        float bodyMaxYTolerance = 0.15f,
        bool bindPose = true)
    {
        Assert.Equal(bind.Elements.Count, partIds.Count);
        TransformWorldExtents(bind, partIds, out var bodyMinY, out var bodyMaxY, out var tentacleMinY, out var tentacleMaxY);

        Assert.InRange(bodyMaxY, expectedBodyMaxY - bodyMaxYTolerance, expectedBodyMaxY + bodyMaxYTolerance);

        // IR ghast root carries deep negative ModelTransforms Y; body shell sits far below preview origin (Skip LER).
        Assert.True(bodyMaxY < -40f, $"IR ghast body shell should sit far below origin without LER (bodyMaxY={bodyMaxY:F3})");

        AssertTentaclesProtrudeOutsideBodyShell(bind, partIds);
        AssertGhastTentaclesDoNotPitchUpThroughBodyTop(bind, partIds);

        var hullGap = tentacleMinY - bodyMaxY;
        if (bindPose)
        {
            Assert.True(hullGap < 0.35f, $"tentacles should stay coupled under body shell (hullGap={hullGap:F3})");
        }
        else
        {
            Assert.True(
                tentacleMinY <= bodyMaxY + 0.35f,
                $"animated tentacles should stay coupled under body shell (tentacleMinY={tentacleMinY:F3}, bodyMaxY={bodyMaxY:F3})");
            Assert.True(hullGap < 0.35f, $"animated tentacles detached under setupAnim (gap={hullGap:F3})");
        }
    }

    public static int FindBodyElementIndex(IReadOnlyList<string> partIds)
    {
        for (var i = 0; i < partIds.Count; i++)
        {
            if (string.Equals(partIds[i], "body", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void TransformWorldExtents(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds,
        out float bodyMinY,
        out float bodyMaxY,
        out float tentacleMinY,
        out float tentacleMaxY)
    {
        bodyMinY = float.PositiveInfinity;
        bodyMaxY = float.NegativeInfinity;
        tentacleMinY = float.PositiveInfinity;
        tentacleMaxY = float.NegativeInfinity;

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            TransformWorldCorners(mesh.Elements[i], out var min, out var max);
            var id = partIds[i];
            if (string.Equals(id, "body", StringComparison.OrdinalIgnoreCase))
            {
                bodyMinY = MathF.Min(bodyMinY, min.Y);
                bodyMaxY = MathF.Max(bodyMaxY, max.Y);
            }
            else if (id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                tentacleMinY = MathF.Min(tentacleMinY, min.Y);
                tentacleMaxY = MathF.Max(tentacleMaxY, max.Y);
            }
        }

        Assert.True(float.IsFinite(bodyMaxY), "body part missing from mesh");
        Assert.True(float.IsFinite(tentacleMinY), "tentacle parts missing from mesh");
    }

    /// <summary>
    /// Un-pitched +Y tentacle cuboids sit fully inside the body AABB and depth-occlude in Explore bind pose.
    /// </summary>
    public static void AssertTentaclesProtrudeOutsideBodyShell(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds)
    {
        var bodyIdx = FindBodyElementIndex(partIds);
        Assert.True(bodyIdx >= 0, "body part missing");
        TransformWorldCorners(mesh.Elements[bodyIdx], out var bodyMin, out var bodyMax);

        var protrudes = false;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (!partIds[i].StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TransformWorldCorners(mesh.Elements[i], out var tMin, out var tMax);
            if (tMin.X < bodyMin.X - 0.05f || tMax.X > bodyMax.X + 0.05f ||
                tMin.Y < bodyMin.Y - 0.05f || tMax.Y > bodyMax.Y + 0.05f ||
                tMin.Z < bodyMin.Z - 0.05f || tMax.Z > bodyMax.Z + 0.05f)
            {
                protrudes = true;
                break;
            }
        }

        Assert.True(protrudes, "tentacles must pitch outside the body shell (bind pose xRot)");
    }

    /// <summary>
    /// Column-root LER + −Y tentacle reorient flips hang direction: tentacle tops land above body top (+Y into shell).
    /// </summary>
    public static void AssertGhastTentaclesDoNotPitchUpThroughBodyTop(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds)
    {
        TransformWorldExtents(mesh, partIds, out _, out var bodyMaxY, out _, out var tentacleMaxY);
        Assert.True(
            tentacleMaxY <= bodyMaxY + 0.5f,
            $"tentacles must hang from body underside, not pitch up through top (tentacleMaxY={tentacleMaxY:F3}, bodyMaxY={bodyMaxY:F3})");
    }

    private static void AssertWorldAabbClose(ModelElement el, Vector3 expectedMin, Vector3 expectedMax, float eps)
    {
        TransformWorldCorners(el, out var min, out var max);
        var detail = $"expectedMin={expectedMin} actualMin={min} expectedMax={expectedMax} actualMax={max}";
        Assert.True(MathF.Abs(expectedMin.X - min.X) <= eps, detail);
        Assert.True(MathF.Abs(expectedMin.Y - min.Y) <= eps, detail);
        Assert.True(MathF.Abs(expectedMin.Z - min.Z) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.X - max.X) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.Y - max.Y) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.Z - max.Z) <= eps, detail);
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
}
