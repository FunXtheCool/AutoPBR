using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Locks multi-part CleanRoom rigs so rotation vs offset mistakes cannot drift assemblies.
/// Landmarks: boat hull, minecart, ghast (body + tentacle), guardian (+ elder scale law), bee thorax, phantom body.
/// Per-element bounds use the same transform semantics as <see cref="MinecraftModelBaker"/>.
/// Living mobs fold <c>LivingEntityRenderer</c> <c>scale(-1,-1,1)</c> as <c>worldRoot * LocalToParent</c>, so world AABB X/Y extrema swap sign (Z unchanged).
/// </summary>
public sealed class EntityTextureParityAssemblyCohesionTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Fact]
    public void ParityCatalog_AllManifestPaths_BuildStaticMesh_IdlePose()
    {
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        Assert.NotEmpty(paths);
        var runtime = EntityModelRuntimeFactory.Create();
        foreach (var path in paths)
        {
            Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
            Assert.NotEmpty(model.Elements);
        }
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png", 58f, 66f)]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png", 58f, 66f)]
    public void BoatFamily_Assembly_OverallWorldDiagonal_IsTight(string path, float minDiag, float maxDiag)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(model.Elements.Count >= 5, $"{path}: expected hull elements");

        var unionDiag = UnionWorldAabbDiagonal(model);
        Assert.InRange(unionDiag, minDiag, maxDiag);
    }

    [Fact]
    public void BambooRaft_Assembly_OverallWorldDiagonal_IsTight()
    {
        const string path = "assets/minecraft/textures/entity/boat/bamboo.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var unionDiag = UnionWorldAabbDiagonal(model);
        Assert.InRange(unionDiag, 45f, 58f);
    }

    [Fact]
    public void Minecart_WorldBounds_AndPanelCenters_StayCoherent()
    {
        const string path = "assets/minecraft/textures/entity/minecart/minecart.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(5, model.Elements.Count);

        // Panels meet at edges; world AABBs still leave small air gaps — use center dispersion instead.
        var maxCenterDist = MaxPairwiseWorldAabbCenterDistance(model, 0, model.Elements.Count);
        Assert.True(maxCenterDist <= 18.25f, $"max center distance {maxCenterDist}");

        var unionDiag = UnionWorldAabbDiagonal(model);
        Assert.InRange(unionDiag, 26f, 29f);

        // MinecartModel landmarks (idle): identify floor by 20x16x2 span and the +Z side wall by center-Z.
        ModelElement? floor = null;
        var foundFloor = false;
        foreach (var el in model.Elements)
        {
            var sx = MathF.Abs(el.To[0] - el.From[0]);
            var sy = MathF.Abs(el.To[1] - el.From[1]);
            var sz = MathF.Abs(el.To[2] - el.From[2]);
            if (MathF.Abs(sx - 20f) < 0.01f && MathF.Abs(sy - 16f) < 0.01f && MathF.Abs(sz - 2f) < 0.01f)
            {
                floor = el;
                foundFloor = true;
            }
        }

        Assert.True(foundFloor, "expected floor panel 20x16x2");
        TransformWorldCorners(floor!, out var floorMin, out var floorMax);
        Assert.Equal(20f, floorMax.X - floorMin.X, 0.08f);
        Assert.Equal(2f, floorMax.Y - floorMin.Y, 0.08f);
        Assert.Equal(16f, floorMax.Z - floorMin.Z, 0.08f);
    }

    [Fact]
    public void BoatOak_BottomSlab_HasExpectedWorldLandmark()
    {
        const string path = "assets/minecraft/textures/entity/boat/oak.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(
            CleanRoomEntityModelRuntime.TryBuildCleanRoomParityCatalogMeshForTests(
                "Boat",
                path,
                Profile26,
                out var clean),
            path);
        var irBottom = FindBoatHullBottomSlab(model);
        var cleanBottom = FindBoatHullBottomSlab(clean);
        TransformWorldCorners(irBottom, out var irMin, out var irMax);
        TransformWorldCorners(cleanBottom, out var cleanMin, out var cleanMax);
        AssertWorldCornerClose(irMin, cleanMin, 0.05f);
        AssertWorldCornerClose(irMax, cleanMax, 0.05f);
    }

    private static void AssertWorldCornerClose(Vector3 actual, Vector3 expected, float tolerance)
    {
        Assert.True(Vector3.Distance(actual, expected) <= tolerance,
            $"corner delta {Vector3.Distance(actual, expected):G6} exceeds {tolerance:G6} (actual={actual}, expected={expected})");
    }

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
    public void Ghast_BodyAndFirstTentacle_WorldLandmarks()
    {
        const string path = "assets/minecraft/textures/entity/ghast/ghast.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(model.Elements.Count >= 10, "body + 9 tentacles");
        var shardPath = GhastPreviewTestLandmarks.RequireOkCommittedShardPath(GhastPreviewTestLandmarks.MonsterJvm);
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(
            GhastPreviewTestLandmarks.MonsterJvm,
            shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 32) with
            {
                OfficialJvmName = GhastPreviewTestLandmarks.MonsterJvm,
            });
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            model,
            partIds,
            GhastPreviewTestLandmarks.MonsterJvm,
            animationTimeSeconds: 0f);
    }

    [Fact]
    public void Guardian_Normal_CoreAndSpikeLandmarks()
    {
        const string path = "assets/minecraft/textures/entity/guardian/guardian.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(22, model.Elements.Count);
        AssertWorldAabbClose(model.Elements[0], new Vector3(-6f, -22f, -8f), new Vector3(6f, -10f, 8f), 0.08f);
        // Upper lid slab (spinePulse=0), axis-aligned.
        AssertWorldAabbClose(model.Elements[4], new Vector3(-6f, -24f, -6f), new Vector3(6f, -22f, 6f), 0.08f);
    }

    [Fact]
    public void GuardianElder_Core_IsUniformlyScaledVersusNormal()
    {
        const string normalPath = "assets/minecraft/textures/entity/guardian/guardian.png";
        const string elderPath = "assets/minecraft/textures/entity/guardian/guardian_elder.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(normalPath, Profile26, 0f, 0f, out var normal), normalPath);
        Assert.True(runtime.TryBuildStaticMesh(elderPath, Profile26, 0f, 0f, out var elder), elderPath);
        TransformWorldCorners(normal.Elements[0], out var nMin, out var nMax);
        TransformWorldCorners(elder.Elements[0], out var eMin, out var eMax);
        var nCenter = (nMin + nMax) * 0.5f;
        var eCenter = (eMin + eMax) * 0.5f;
        Assert.Equal(nCenter.X, eCenter.X, 0.2f);
        Assert.Equal(nCenter.Y, eCenter.Y, 0.2f);
        Assert.Equal(nCenter.Z, eCenter.Z, 0.2f);
        var nHalf = (nMax - nMin) * 0.5f;
        var eHalf = (eMax - eMin) * 0.5f;
        const float scale = 2.35f;
        Assert.Equal(nHalf.X * scale, eHalf.X, 0.15f);
        Assert.Equal(nHalf.Y * scale, eHalf.Y, 0.15f);
        Assert.Equal(nHalf.Z * scale, eHalf.Z, 0.15f);
    }

    [Fact]
    public void Bee_Thorax_WorldLandmark()
    {
        const string path = "assets/minecraft/textures/entity/bee/bee.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(model.Elements.Count >= 5);
        // Runtime geometry IR thorax after vanilla LER column-root scale (Y negated).
        AssertWorldAabbClose(model.Elements[0], new Vector3(-3.5f, -22f, -5f), new Vector3(3.5f, -15f, 5f), 0.08f);
    }

    [Fact]
    public void Phantom_Body_WorldLandmark_Idle()
    {
        const string path = "assets/minecraft/textures/entity/phantom/phantom.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        AssertPhantomJavaBodyBoxWorldAabb(FindPhantomBodyCuboid(model));
    }

    private static ModelElement FindPhantomBodyCuboid(MergedJavaBlockModel model)
    {
        foreach (var e in model.Elements)
        {
            var edges = new[]
            {
                MathF.Abs(e.To[0] - e.From[0]),
                MathF.Abs(e.To[1] - e.From[1]),
                MathF.Abs(e.To[2] - e.From[2]),
            };
            Array.Sort(edges);
            // Body extents 5×3×9 → sorted edge lengths (3, 5, 9).
            if (MathF.Abs(edges[0] - 3f) < 0.08f && MathF.Abs(edges[1] - 5f) < 0.08f && MathF.Abs(edges[2] - 9f) < 0.08f)
            {
                return e;
            }
        }

        var sb = new StringBuilder("Expected phantom body element (sorted spans 3×5×9). Got ");
        for (var i = 0; i < model.Elements.Count; i++)
        {
            var e = model.Elements[i];
            var dx = MathF.Abs(e.To[0] - e.From[0]);
            var dy = MathF.Abs(e.To[1] - e.From[1]);
            var dz = MathF.Abs(e.To[2] - e.From[2]);
            sb.Append(CultureInfo.InvariantCulture, $"[{i} {dx:G9},{dy:G9},{dz:G9}] ");
        }

        throw new Xunit.Sdk.XunitException(sb.ToString());
    }

    /// <summary>Runtime geometry IR phantom body box at idle after lifted body pose composition.</summary>
    private static void AssertPhantomJavaBodyBoxWorldAabb(ModelElement bodyEl)
    {
        AssertWorldAabbClose(
            bodyEl,
            new Vector3(-2f, -1.0948375f, -8.059867f),
            new Vector3(3f, 2.7886758f, 1.194671f),
            0.08f);
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

    private static Vector3 WorldAabbCenter(ModelElement el)
    {
        TransformWorldCorners(el, out var min, out var max);
        return (min + max) * 0.5f;
    }

    private static float MaxPairwiseWorldAabbCenterDistance(MergedJavaBlockModel model, int start, int count)
    {
        var end = start + count;
        if (count <= 1 || start < 0 || end > model.Elements.Count)
        {
            return 0f;
        }

        var centers = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            centers[i] = WorldAabbCenter(model.Elements[start + i]);
        }

        var maxD = 0f;
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                maxD = MathF.Max(maxD, Vector3.Distance(centers[i], centers[j]));
            }
        }

        return maxD;
    }

    private static float UnionWorldAabbDiagonal(MergedJavaBlockModel model)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var emin, out var emax);
            min = Vector3.Min(min, emin);
            max = Vector3.Max(max, emax);
        }

        return Vector3.Distance(min, max);
    }
}
