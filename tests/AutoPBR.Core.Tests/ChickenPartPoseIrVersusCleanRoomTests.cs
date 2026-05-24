using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Xunit;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Explores <b>part pose</b> from vanilla geometry IR vs CleanRoom <see cref="ModelElement.LocalToParent"/>
/// after stripping the preview LER mirror (<c>scale(-1,-1,1)</c>).
/// </summary>
/// <remarks>
/// Geometry IR lists <c>beak</c> / <c>red_thing</c> as root siblings with zero translation; in Java they are head children.
/// CleanRoom folds head look into the same <c>headPose</c> matrix for head, beak, and wattle cuboids, so merged
/// bind-pose matrices for those three elements match <b>head</b> IR pose, not the beak/wattle IR rows.
/// </remarks>
public sealed class ChickenPartPoseIrVersusCleanRoomTests
{
    private static string ContentPath(params string[] segments) =>
        Path.Combine([AppContext.BaseDirectory, .. segments]);

    /// <summary>Matches <see cref="CleanRoomEntityModelRuntime"/> <c>EntityParityTemplate.Er</c> (Z then Y then X).</summary>
    private static Matrix4x4 Er(float xRad, float yRad, float zRad) =>
        Matrix4x4.Multiply(
            Matrix4x4.Multiply(Matrix4x4.CreateRotationZ(zRad), Matrix4x4.CreateRotationY(yRad)),
            Matrix4x4.CreateRotationX(xRad));

    private static Matrix4x4 Mul(Matrix4x4 a, Matrix4x4 b) => Matrix4x4.Multiply(a, b);

    private static Matrix4x4 Translation(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

    /// <summary>IR <c>pose</c> as a single part-local matrix: <c>T · Er</c> (same as head/body chains in CleanRoom).</summary>
    private static Matrix4x4 MatrixFromIrPartPose(JsonElement pose)
    {
        var t = pose.GetProperty("translation");
        var r = pose.GetProperty("rotationEulerRad");
        var tx = (float)t[0].GetDouble();
        var ty = (float)t[1].GetDouble();
        var tz = (float)t[2].GetDouble();
        var rx = (float)r[0].GetDouble();
        var ry = (float)r[1].GetDouble();
        var rz = (float)r[2].GetDouble();
        return Mul(Translation(tx, ty, tz), Er(rx, ry, rz));
    }

    private static Dictionary<string, Matrix4x4> LoadChicken26IrBindPoseMatrices()
    {
        var path = ContentPath("docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.ChickenModel.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var ch in doc.RootElement.GetProperty("roots")[0].GetProperty("children").EnumerateArray())
        {
            var id = ch.GetProperty("id").GetString()!;
            map[id] = MatrixFromIrPartPose(ch.GetProperty("pose"));
            if (!string.Equals(id, "head", StringComparison.Ordinal) ||
                !ch.TryGetProperty("children", out var headKids) ||
                headKids.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var sub in headKids.EnumerateArray())
            {
                var sid = sub.GetProperty("id").GetString()!;
                map[sid] = MatrixFromIrPartPose(sub.GetProperty("pose"));
            }
        }

        return map;
    }

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps)
    {
        return Math.Abs(a.M11 - b.M11) <= eps && Math.Abs(a.M12 - b.M12) <= eps && Math.Abs(a.M13 - b.M13) <= eps &&
               Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps && Math.Abs(a.M22 - b.M22) <= eps && Math.Abs(a.M23 - b.M23) <= eps &&
               Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps && Math.Abs(a.M32 - b.M32) <= eps && Math.Abs(a.M33 - b.M33) <= eps &&
               Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps && Math.Abs(a.M43 - b.M43) <= eps &&
               Math.Abs(a.M44 - b.M44) <= eps;
    }

    [Fact]
    public void Bind_pose_stripped_merged_matrices_match_ir_for_root_aligned_parts()
    {
        GeometryIrParityPolicy.ResetForTests();
        var ir = LoadChicken26IrBindPoseMatrices();
        var headPoseIr = ir["head"];
        var bodyPoseIr = ir["body"];
        var rightLegIr = ir["right_leg"];
        var rightWingIr = ir["right_wing"];
        var leftWingIr = ir["left_wing"];
        var beakLeafIr = ir["beak"];

        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/chicken/chicken.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var merged));
        Assert.Equal(8, merged.Elements.Count);

        var worldRoot = Matrix4x4.CreateScale(-1f, -1f, 1f);
        Assert.True(Matrix4x4.Invert(worldRoot, out var invWorld));
        Matrix4x4 Strip(in Matrix4x4 m) => Mul(invWorld, m);

        // BuildChicken order: head slab, beak, wattle, body, R leg, L leg, R wing, L wing — all angles zero at bind.
        var m0 = Strip(merged.Elements[0].LocalToParent);
        var m1 = Strip(merged.Elements[1].LocalToParent);
        var m2 = Strip(merged.Elements[2].LocalToParent);
        var m3 = Strip(merged.Elements[3].LocalToParent);
        var m4 = Strip(merged.Elements[4].LocalToParent);
        var m5 = Strip(merged.Elements[5].LocalToParent);
        var m6 = Strip(merged.Elements[6].LocalToParent);
        var m7 = Strip(merged.Elements[7].LocalToParent);

        const float eps = 1e-4f;
        Assert.True(MatricesClose(m0, headPoseIr, eps), "head slab");
        Assert.True(MatricesClose(m3, bodyPoseIr, eps), "body");

        // Geometry IR part poses come from createBodyLayer only. Preview BuildChicken always applies setupAnim:
        // legs use cos(limbSwing·0.6662+phase)·1.4·amount (non-zero at t=0); wings use (sin(flap)+1)·flapSpeed.
        // So IR right_leg / wing poses do not match merged matrices at animationTimeSeconds == 0.
        Assert.False(MatricesClose(m4, rightLegIr, 1e-2f), "right leg preview includes walk-cycle pitch vs static IR");
        Assert.False(MatricesClose(m6, rightWingIr, 1e-2f), "right wing preview includes flap bias vs static IR");
        Assert.False(MatricesClose(m7, leftWingIr, 1e-2f), "left wing preview includes flap bias vs static IR");

        // Beak / wattle use head chain in CleanRoom, not IR leaf poses (IR has identity translation on those nodes).
        Assert.True(MatricesClose(m1, headPoseIr, eps), "beak merged pose should match head IR pose");
        Assert.True(MatricesClose(m2, headPoseIr, eps), "wattle merged pose should match head IR pose");
        Assert.False(
            MatricesClose(m1, beakLeafIr, 0.05f),
            "beak IR leaf pose is not the merged model matrix (flat IR vs head-local Java).");

        // Left leg: mirrored root X vs right_leg; at anim 0 still gets quadruped pitch (inverted phase vs right).
        var limbSwingAmount = Math.Clamp(0.22f + 0f * 0.18f + 0f * 0.12f, 0.05f, 0.95f);
        var leftPitch = MathF.Cos(0f * 0.6662f + MathF.PI) * 1.4f * limbSwingAmount;
        var leftLegExpected = Mul(Translation(1f, 19f, 1f), Er(leftPitch, 0f, 0f));
        Assert.True(MatricesClose(m5, leftLegExpected, eps), "left leg at anim 0 matches inverted-phase cosine leg pitch");
    }

    private static (Matrix4x4 head, Matrix4x4 body) LoadColdChicken26IrHeadBodyPoses()
    {
        var path = ContentPath("docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.ColdChickenModel.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Matrix4x4? headM = null, bodyM = null;
        foreach (var ch in doc.RootElement.GetProperty("roots")[0].GetProperty("children").EnumerateArray())
        {
            var id = ch.GetProperty("id").GetString()!;
            var m = MatrixFromIrPartPose(ch.GetProperty("pose"));
            if (string.Equals(id, "head", StringComparison.Ordinal))
            {
                headM = m;
            }
            else if (string.Equals(id, "body", StringComparison.Ordinal))
            {
                bodyM = m;
            }
        }

        Assert.True(headM.HasValue && bodyM.HasValue, "ColdChicken IR should define head and body poses");
        return (headM!.Value, bodyM!.Value);
    }

    /// <summary>
    /// <c>ColdChickenModel</c> IR DFS: head (2), beak, wattle, body (2), legs, wings.
    /// Stripped matrices for head/body cuboids match IR part poses at bind; legs/wings include preview idle drivers.
    /// </summary>
    [Fact]
    public void Cold_chicken_bind_pose_stripped_head_body_match_cold_ir()
    {
        GeometryIrParityPolicy.ResetForTests();
        var (headPoseIr, bodyPoseIr) = LoadColdChicken26IrHeadBodyPoses();
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/chicken/chicken_cold.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var merged));
        Assert.Equal(10, merged.Elements.Count);

        var worldRoot = Matrix4x4.CreateScale(-1f, -1f, 1f);
        Assert.True(Matrix4x4.Invert(worldRoot, out var invWorld));
        Matrix4x4 Strip(in Matrix4x4 m) => Mul(invWorld, m);

        const float eps = 1e-4f;
        Assert.True(MatricesClose(Strip(merged.Elements[0].LocalToParent), headPoseIr, eps), "cold head main");
        Assert.True(MatricesClose(Strip(merged.Elements[1].LocalToParent), headPoseIr, eps), "cold hood");
        Assert.True(MatricesClose(Strip(merged.Elements[2].LocalToParent), headPoseIr, eps), "cold beak under head");
        Assert.True(MatricesClose(Strip(merged.Elements[3].LocalToParent), headPoseIr, eps), "cold wattle under head");
        Assert.True(MatricesClose(Strip(merged.Elements[4].LocalToParent), bodyPoseIr, eps), "cold body main");
        Assert.True(MatricesClose(Strip(merged.Elements[5].LocalToParent), bodyPoseIr, eps), "cold body crest");
    }

    [Fact]
    public void Head_pitch_from_idle_phase_stripped_matrix_tracks_ir_head_pose_plus_Euler()
    {
        GeometryIrParityPolicy.ResetForTests();
        var ir = LoadChicken26IrBindPoseMatrices();
        var headBase = ir["head"];

        const float pitch = 0.11f;
        var headAnimatedIr = Mul(headBase, Er(pitch, 0f, 0f));

        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/chicken/chicken.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        // Drive head look via BuildChicken: headPitchDegrees = idle*8 + wave*5, headYaw = wave*10; anim 0 => wave = 0.
        var idle = pitch / (8f * (MathF.PI / 180f));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: idle, animationTimeSeconds: 0f, out var merged));
        var worldRoot = Matrix4x4.CreateScale(-1f, -1f, 1f);
        Assert.True(Matrix4x4.Invert(worldRoot, out var invWorld));
        var m0 = Mul(invWorld, merged.Elements[0].LocalToParent);
        const float eps = 2e-3f;
        Assert.True(MatricesClose(m0, headAnimatedIr, eps), "head slab should include Er(pitch, yaw, 0) on top of IR base");
    }
}
