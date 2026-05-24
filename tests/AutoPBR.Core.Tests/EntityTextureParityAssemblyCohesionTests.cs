using System.Globalization;
using System.Numerics;
using System.Text;

using AutoPBR.Core.Preview;

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
    [InlineData("assets/minecraft/textures/entity/boat/oak.png")]
    [InlineData("assets/minecraft/textures/entity/boat/bamboo.png")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png")]
    public void BoatFamily_Assembly_OverallWorldDiagonal_IsTight(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(model.Elements.Count >= 5, $"{path}: expected hull elements");

        // Paddles (and chest on chest_boat) stretch the union diagonal; band matches the current vanilla-tuned rig.
        var unionDiag = UnionWorldAabbDiagonal(model);
        Assert.InRange(unionDiag, 58f, 66f);
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
        Assert.InRange(unionDiag, 26f, 30f);

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
        TransformWorldCorners(model.Elements[0], out var min, out _);
        Assert.Equal(1.5f, min.Y, 0.15f);
        Assert.Equal(-5.5f, min.Z, 0.35f);
        Assert.Equal(-14f, min.X, 0.35f);
    }

    [Fact]
    public void Ghast_BodyAndFirstTentacle_WorldLandmarks()
    {
        const string path = "assets/minecraft/textures/entity/ghast/ghast.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.True(model.Elements.Count >= 10, "body + 9 tentacles");
        // GhastModel body cuboid gch: corners (0, 9.6, 0)-(16, 25.6, 16) in flat rig space; then LER scale(-1,-1,1) on world root.
        AssertWorldAabbClose(model.Elements[0], new Vector3(-16f, -25.6f, 0f), new Vector3(0f, -9.6f, 16f), 0.08f);
        // Tentacle 0: Java corners (3,-8,3)-(5,0,5); RigBuilder applies Rx(pitch) about pivot (4,24.6,4) with idle tentacleSway=0.
        AssertAxisAlignedBoxWorldAabb(
            model.Elements[1],
            WithLivingEntityPreviewWorldRoot(ExpectedGhastTentacle0MeshLocal(0f)),
            new Vector3(3f, -8f, 3f),
            new Vector3(5f, 0f, 5f),
            0.08f);
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
        // BeeModel thorax: corners (4.5, 15, 3)-(11.5, 22, 13); wings animate later indices; LER on world root.
        AssertWorldAabbClose(model.Elements[0], new Vector3(-11.5f, -22f, 3f), new Vector3(-4.5f, -15f, 13f), 0.08f);
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

    /// <summary>Java addBox(−3,−2,−8, 5,3,9) → corners (−3,−2,−8)-(2,1,1); baked <c>LocalToParent</c> is <c>scale(-1,-1,1) * T(8,18,8) * Rx(−0.1)</c> like <see cref="CleanRoomEntityModelRuntime"/>.</summary>
    private static void AssertPhantomJavaBodyBoxWorldAabb(ModelElement bodyEl)
    {
        var mat = WithLivingEntityPreviewWorldRoot(
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 18f, 8f), Matrix4x4.CreateRotationX(-0.1f)));
        var mn = new Vector3(float.MaxValue);
        var mx = new Vector3(float.MinValue);
        foreach (var x in new[] { -3f, 2f })
        {
            foreach (var y in new[] { -2f, 1f })
            {
                foreach (var z in new[] { -8f, 1f })
                {
                    var w = Vector3.Transform(new Vector3(x, y, z), mat);
                    mn = Vector3.Min(mn, w);
                    mx = Vector3.Max(mx, w);
                }
            }
        }

        AssertWorldAabbClose(bodyEl, mn, mx, 0.08f);
    }

    /// <summary>Matches <see cref="CleanRoomEntityModelRuntime"/> RigBuilder Euler + pivot composition for ghast tentacle 0 at idle sway.</summary>
    private static Matrix4x4 ExpectedGhastTentacle0MeshLocal(float tentacleSway)
    {
        var pitch = 0.4f + 0.2f * MathF.Sin(tentacleSway * 1.5f + 0 * 0.3f);
        var pivot = new Vector3(4f, 24.6f, 4f);
        var euler = Matrix4x4.Multiply(
            Matrix4x4.CreateRotationZ(0f),
            Matrix4x4.Multiply(Matrix4x4.CreateRotationY(0f), Matrix4x4.CreateRotationX(pitch)));
        return Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(pivot),
            Matrix4x4.Multiply(euler, Matrix4x4.CreateTranslation(-pivot)));
    }

    private static void AssertAxisAlignedBoxWorldAabb(ModelElement el, Matrix4x4 meshLocal, Vector3 rawMin, Vector3 rawMax, float eps)
    {
        var mn = new Vector3(float.MaxValue);
        var mx = new Vector3(float.MinValue);
        foreach (var x in new[] { rawMin.X, rawMax.X })
        {
            foreach (var y in new[] { rawMin.Y, rawMax.Y })
            {
                foreach (var z in new[] { rawMin.Z, rawMax.Z })
                {
                    var w = Vector3.Transform(new Vector3(x, y, z), meshLocal);
                    mn = Vector3.Min(mn, w);
                    mx = Vector3.Max(mx, w);
                }
            }
        }

        AssertWorldAabbClose(el, mn, mx, eps);
    }

    /// <summary>Matches clean-room default LER fold (non-quadruped): <c>worldRoot * LocalToParent</c> with <c>scale(-1,-1,1)</c>.</summary>
    private static Matrix4x4 WithLivingEntityPreviewWorldRoot(Matrix4x4 localToParent) =>
        Matrix4x4.Multiply(Matrix4x4.CreateScale(-1f, -1f, 1f), localToParent);

    private static void AssertWorldAabbClose(ModelElement el, Vector3 expectedMin, Vector3 expectedMax, float eps)
    {
        TransformWorldCorners(el, out var min, out var max);
        Assert.Equal(expectedMin.X, min.X, eps);
        Assert.Equal(expectedMin.Y, min.Y, eps);
        Assert.Equal(expectedMin.Z, min.Z, eps);
        Assert.Equal(expectedMax.X, max.X, eps);
        Assert.Equal(expectedMax.Y, max.Y, eps);
        Assert.Equal(expectedMax.Z, max.Z, eps);
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
