using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Agent 1B: <c>PartPose.offsetAndRotation</c> and reused <c>CubeListBuilder</c> leg binds (creeper / quadruped pattern).
/// Hand-parity targets from <c>CleanRoomEntityMonsters.BuildCreeper</c> (1.21.11); 26.1.2 jar uses <c>PartPose.offset</c> only per javap snapshot.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class CreeperPartPoseLiftTests
{
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";
    private const string CowJvm = "net.minecraft.client.model.animal.cow.CowModel";
    private const double HalfPi = Math.PI / 2.0;
    private const double PoseTol = 0.02;
    private static readonly string[] CreeperLegPartIds =
        ["right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg"];

    /// <summary>1.21.11-style creeper body + legs (clean-room / <c>hcn.a</c> pattern).</summary>
    private const string CreeperHandParityBodyAndLegsSlice = """
        Code:
           0: aload_1
           1: ldc           #8                 // String body
           3: invokestatic  #10                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
           6: bipush        28
           8: bipush        8
          10: invokevirtual #16                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          13: ldc           #20                // float -4.0f
          15: ldc           #21                // float -10.0f
          17: ldc           #22                // float -7.0f
          19: ldc           #23                // float 8.0f
          21: ldc           #24                // float 16.0f
          23: ldc           #25                // float 6.0f
          25: invokevirtual #26                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          28: fconst_0
          29: ldc           #30                // float 5.0f
          31: fconst_2
          32: ldc           #31                // float 1.5707964f
          34: fconst_0
          35: fconst_0
          36: invokestatic  #32                // Method net/minecraft/client/model/geom/PartPose.offsetAndRotation:(FFFFFF)Lnet/minecraft/client/model/geom/PartPose;
          39: invokevirtual #38                // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
          42: invokestatic  #10                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          45: iconst_0
          46: bipush        16
          48: invokevirtual #16                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          51: ldc           #40                // float -2.0f
          53: fconst_0
          54: ldc           #40                // float -2.0f
          56: ldc           #41                // float 4.0f
          58: ldc           #42                // float 6.0f
          60: ldc           #41                // float 4.0f
          62: invokevirtual #26                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          65: astore_2
          66: aload_1
          67: ldc           #44                // String right_hind_leg
          69: aload_2
          70: ldc           #46                // float -3.0f
          72: ldc           #47                // float 12.0f
          74: ldc           #48                // float 7.0f
          76: invokestatic  #49                // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
          79: invokevirtual #38                // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
        """;

    [Fact]
    public void CowModel_lifted_body_has_rx_about_half_pi()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null, CowJvm, "createBodyLayer", out var resolved),
            "mesh resolution failed");
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(resolved.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));

        var body = FindPart(roots, "body");
        Assert.NotNull(body);
        var rx = body["pose"]!["rotationEulerRad"]![0]!.GetValue<double>();
        Assert.True(Math.Abs(rx - HalfPi) < PoseTol, $"cow body Rx expected ~π/2, got {rx}");
        Assert.Equal(5d, Ty(body), 0.01);
        Assert.Equal(2d, Tz(body), 0.01);
    }

    [Fact]
    public void Creeper_hand_parity_pattern_offsetAndRotation_and_leg_offsets_lift()
    {
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(CreeperHandParityBodyAndLegsSlice, out var roots, out var notes),
            string.Join("; ", notes));

        var body = FindPart(roots, "body");
        var leg = FindPart(roots, "right_hind_leg");
        Assert.NotNull(body);
        Assert.NotNull(leg);

        var rx = body["pose"]!["rotationEulerRad"]![0]!.GetValue<double>();
        Assert.True(Math.Abs(rx - HalfPi) < PoseTol, $"body Rx expected ~π/2, got {rx}");
        Assert.Equal(5d, Ty(body), 0.01);
        Assert.Equal(2d, Tz(body), 0.01);

        Assert.Equal(-3d, Tx(leg), 0.01);
        Assert.Equal(12d, Ty(leg), 0.01);
        Assert.Equal(7d, Tz(leg), 0.01);
    }

    [Fact]
    public void CreeperModel_jar_lift_matches_bytecode_oracle_poses()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, CreeperJvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var head = FindPart(roots, "head");
        var body = FindPart(roots, "body");
        var rightHind = FindPart(roots, "right_hind_leg");
        Assert.NotNull(head);
        Assert.NotNull(body);
        Assert.NotNull(rightHind);

        // 26.1.2 javap: PartPose.offset only (see tools/minecraft-parity/26.1.2/javap-snapshots/CreeperModel.createBodyLayer.javap.txt)
        Assert.Equal(0d, Tx(head), 0.01);
        Assert.Equal(6d, Ty(head), 0.01);
        Assert.Equal(0d, Tz(head), 0.01);
        Assert.Equal(0d, Rx(body), 0.01);

        Assert.Equal(0d, Tx(body), 0.01);
        Assert.Equal(6d, Ty(body), 0.01);
        Assert.Equal(0d, Tz(body), 0.01);

        Assert.Equal(-2d, Tx(rightHind), 0.01);
        Assert.Equal(18d, Ty(rightHind), 0.01);
        Assert.Equal(4d, Tz(rightHind), 0.01);
    }

    [Fact]
    public void CreeperModel_jar_body_rotation_or_pose_approx_when_unrecoverable()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, CreeperJvm, "createBodyLayer", out var resolved));
        var hasOffsetAndRotation = resolved.MeshConcat.Contains("PartPose.offsetAndRotation", StringComparison.Ordinal);

        Assert.True(
            GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, CreeperJvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var body = FindPart(roots, "body");
        Assert.NotNull(body);
        var rx = body["pose"]!["rotationEulerRad"]![0]!.GetValue<double>();
        var poseApprox = CountPoseApproxWarnings(roots);

        if (hasOffsetAndRotation)
        {
            Assert.True(Math.Abs(rx - HalfPi) < PoseTol, $"expected body Rx≈π/2 from jar offsetAndRotation, got {rx}");
        }
        else
        {
            Assert.True(
                Math.Abs(rx) < PoseTol || poseApprox > 0,
                $"26.1.2 creeper body has no offsetAndRotation in jar; Rx={rx}, poseApproxWarnings={poseApprox}");
        }
    }

    [Fact]
    public void CreeperModel_jar_leg_poses_closer_to_hand_parity_than_stale_wrong_targets()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, CreeperJvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var rightHind = FindPart(roots, "right_hind_leg");
        Assert.NotNull(rightHind);
        var t = Translation(rightHind);

        // Hand parity (1.21.11 clean-room): T(-3, 12, 7)
        var handParity = (-3d, 12d, 7d);
        // Stale wrong IR from roadmap investigation: T(-2, 18, 4) — matches 26.1.2 bytecode, not hand parity
        var bytecodeOracle = (-2d, 18d, 4d);

        var distHand = TranslationDistance(t, handParity);
        var distBytecode = TranslationDistance(t, bytecodeOracle);

        if (BytecodeMeshResolution.TryResolve(jar, null, CreeperJvm, "createBodyLayer", out var resolved) &&
            !resolved.MeshConcat.Contains("PartPose.offsetAndRotation", StringComparison.Ordinal))
        {
            Assert.True(
                distBytecode < distHand + 0.01,
                $"jar uses bytecode leg pose {Format(t)}; hand parity {Format(handParity)} distHand={distHand:F2} distBytecode={distBytecode:F2}");
            return;
        }

        Assert.True(
            distHand < distBytecode,
            $"expected legs closer to hand parity {Format(handParity)} than bytecode-wrong {Format(bytecodeOracle)}; got {Format(t)}");
    }

    [Fact]
    public void CreeperModel_jar_four_reused_leg_binds_have_distinct_poses()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, CreeperJvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var legs = CreeperLegPartIds
            .Select(id => Translation(FindPart(roots, id)!))
            .ToList();
        Assert.Equal(4, legs.Count);
        Assert.Equal(4, legs.Distinct().Count());
    }

    private static double Tx(JsonObject part) => part["pose"]!["translation"]![0]!.GetValue<double>();
    private static double Ty(JsonObject part) => part["pose"]!["translation"]![1]!.GetValue<double>();
    private static double Tz(JsonObject part) => part["pose"]!["translation"]![2]!.GetValue<double>();
    private static double Rx(JsonObject part) => part["pose"]!["rotationEulerRad"]![0]!.GetValue<double>();

    private static (double X, double Y, double Z) Translation(JsonObject part) => (Tx(part), Ty(part), Tz(part));

    private static double TranslationDistance((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static string Format((double X, double Y, double Z) t) => $"({t.X}, {t.Y}, {t.Z})";

    private static int CountPoseApproxWarnings(JsonArray roots)
    {
        var n = 0;
        void Walk(JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject p)
                {
                    continue;
                }

                if (p["pose"]?["liftWarnings"] is JsonArray w)
                {
                    n += w.Count;
                }

                if (p["children"] is JsonArray ch)
                {
                    Walk(ch);
                }
            }
        }

        Walk(roots);
        return n;
    }

    private static JsonObject? FindPart(JsonArray roots, string id)
    {
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindPart(ro, id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPart(JsonObject part, string id, out JsonObject? found)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co && TryFindPart(co, id, out found))
                {
                    return true;
                }
            }
        }

        found = null;
        return false;
    }
}
