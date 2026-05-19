using System.Text.Json;
using System.Text.Json.Nodes;



namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// <c>CubeListBuilder.mirror()</c> / <c>mirror(boolean)</c> before <c>addBox</c> → <c>mirrorU</c> on lifted cuboids.
/// </summary>
public sealed class HumanoidMirrorLiftTests
{
    /// <summary>Subset of <c>HumanoidModel</c> javap: <c>left_arm</c> uses <c>texOffs</c> then <c>mirror()</c> then <c>addBox</c>.</summary>
    private const string LeftArmMirrorSlice = """
    Code:
     176: aload_3
     177: ldc           #42                 // String left_arm
     179: invokestatic  #66                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     182: bipush        40
     184: bipush        16
     186: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     189: invokevirtual #111                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.mirror:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     192: ldc           #114                // float -1.0f
     194: ldc           #106                // float -2.0f
     196: ldc           #106                // float -2.0f
     198: ldc           #108                // float 4.0f
     200: ldc           #107                // float 12.0f
     202: ldc           #108                // float 4.0f
     204: aload_0
     205: invokevirtual #79                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     208: ldc           #115                // float 5.0f
     210: fconst_2
     211: fload_1
     212: fadd
     213: fconst_0
     214: invokestatic  #83                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
     217: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    private const string RightLegNoMirrorSlice = """
    Code:
     221: aload_3
     222: ldc           #47                 // String right_leg
     224: invokestatic  #66                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     227: iconst_0
     228: bipush        16
     230: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     233: ldc           #106                // float -2.0f
     235: fconst_0
     236: ldc           #106                // float -2.0f
     238: ldc           #108                // float 4.0f
     240: ldc           #107                // float 12.0f
     242: ldc           #108                // float 4.0f
     244: aload_0
     245: invokevirtual #79                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     248: ldc           #116                // float -1.9f
     250: ldc           #107                // float 12.0f
     252: fload_1
     253: fadd
     254: fconst_0
     255: invokestatic  #83                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
     258: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary><c>ldc</c> + <c>fstore_*</c> then <c>fload_*</c> operands to <c>PartPose.offset</c>.</summary>
    private const string RightLegPoseUsesFstoredLocalsSlice = """
    Code:
       0: ldc           #125                // float 2.25f
       2: fstore_4
     221: aload_3
     222: ldc           #47                 // String right_leg
     224: invokestatic  #66                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     227: iconst_0
     228: bipush        16
     230: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     233: ldc           #106                // float -2.0f
     235: fconst_0
     236: ldc           #106                // float -2.0f
     238: ldc           #108                // float 4.0f
     240: ldc           #107                // float 12.0f
     242: ldc           #108                // float 4.0f
     244: aload_0
     245: invokevirtual #79                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     248: fload_4
     249: fload_4
     250: fload_4
     251: invokestatic  #83                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
     254: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    private const string MirrorFalseBeforeAddBoxSlice = """
    Code:
       0: aload_1
       1: ldc           #10                 // String body
       3: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
       6: iconst_0
       7: bipush        16
       9: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: invokevirtual #20                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.mirror:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      15: iconst_0
      16: invokevirtual #21                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.mirror:(Z)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      19: ldc           #30                 // float -2.0f
      21: fconst_0
      22: ldc           #30                 // float -2.0f
      24: ldc           #31                 // float 4.0f
      26: ldc           #32                 // float 12.0f
      28: ldc           #31                 // float 4.0f
      30: invokevirtual #16                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      33: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      36: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void TryLift_Sets_mirrorU_on_cuboid_after_texOffs_then_mirror_no_arg()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(LeftArmMirrorSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var part = root["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "left_arm", StringComparison.Ordinal));
        var cub = part["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.True(cub["mirrorU"]!.GetValueKind() == JsonValueKind.True);
    }

    [Fact]
    public void TryLift_Omits_mirrorU_when_mirror_not_called()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(RightLegNoMirrorSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var part = root["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "right_leg", StringComparison.Ordinal));
        var cub = part["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.False(cub.AsObject().ContainsKey("mirrorU"));
    }

    [Fact]
    public void TryLift_Mirror_boolean_false_after_mirror_no_arg_clears_mirrorU()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(MirrorFalseBeforeAddBoxSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var part = root["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "body", StringComparison.Ordinal));
        var cub = part["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.False(cub.AsObject().ContainsKey("mirrorU"));
    }

    [Fact]
    public void TryLift_left_arm_pose_parses_fadd_with_fload_as_placeholder_zero()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(LeftArmMirrorSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var part = roots[0]!.AsObject()["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "left_arm", StringComparison.Ordinal));
        var t = part["pose"]!.AsObject()["translation"]!.AsArray();
        Assert.Equal(5d, t[0]!.GetValue<double>(), 6);
        Assert.Equal(2d, t[1]!.GetValue<double>(), 6);
        Assert.Equal(0d, t[2]!.GetValue<double>(), 6);
        // fload_1 is the createMesh(CubeDeformation, float) param — pre-resolved to 0 (reference bake default).
        var warnings = part["pose"]!["liftWarnings"]?.AsArray().Select(n => (string?)n).ToArray() ?? [];
        Assert.DoesNotContain("unknown_fload_zeroed", warnings);
    }

    [Fact]
    public void TryLift_right_leg_pose_parses_offset_with_fadd_after_constants()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(RightLegNoMirrorSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var part = roots[0]!.AsObject()["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "right_leg", StringComparison.Ordinal));
        var t = part["pose"]!.AsObject()["translation"]!.AsArray();
        Assert.Equal(-1.9, t[0]!.GetValue<double>(), 6);
        Assert.Equal(12d, t[1]!.GetValue<double>(), 6);
        Assert.Equal(0d, t[2]!.GetValue<double>(), 6);
    }

    [Fact]
    public void TryLift_right_leg_pose_resolves_fload_from_fstored_float_local()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(RightLegPoseUsesFstoredLocalsSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var part = roots[0]!.AsObject()["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], "right_leg", StringComparison.Ordinal));
        var t = part["pose"]!.AsObject()["translation"]!.AsArray();
        Assert.Equal(2.25, t[0]!.GetValue<double>(), 6);
        Assert.Equal(2.25, t[1]!.GetValue<double>(), 6);
        Assert.Equal(2.25, t[2]!.GetValue<double>(), 6);
    }
}
