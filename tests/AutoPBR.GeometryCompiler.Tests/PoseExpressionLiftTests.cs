


namespace AutoPBR.GeometryCompiler.Tests;

public sealed class PoseExpressionLiftTests
{
    private const string MathCosOffsetSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #30                 // float -2.0f
      20: fconst_0
      21: ldc           #30                 // float -2.0f
      23: ldc           #31                 // float 4.0f
      25: ldc           #32                 // float 12.0f
      27: ldc           #31                 // float 4.0f
      29: invokevirtual #16                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      32: ldc           #90                 // float 0.7853982f
      34: invokestatic  #91                 // Method java/lang/Math.cos:(F)F
      36: fconst_0
      37: fconst_0
      38: invokestatic  #40                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      41: ldc           #8                  // String fin
      43: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    private const string BlazeLoopPoseSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: iconst_0
      14: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      17: ldc           #30                 // float -2.0f
      19: fconst_0
      20: ldc           #30                 // float -2.0f
      22: ldc           #31                 // float 4.0f
      24: ldc           #32                 // float 8.0f
      26: ldc           #31                 // float 2.0f
      28: invokevirtual #16                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      31: goto          50
      34: ldc           #90                 // float 1.0f
      36: invokestatic  #91                 // Method java/lang/Math.sin:(F)F
      38: fconst_0
      39: fconst_0
      40: invokestatic  #40                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      43: ldc           #8                  // String rod
      45: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void TryLift_math_cos_constant_in_pose_offset()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(MathCosOffsetSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var pose = roots[0]!["children"]!.AsArray()[0]!["pose"]!.AsObject();
        var t = pose["translation"]!.AsArray();
        Assert.InRange(t[0]!.GetValue<double>(), 0.7, 0.71);
        Assert.Equal(0d, t[1]!.GetValue<double>(), 4);
        Assert.Equal(0d, t[2]!.GetValue<double>(), 4);
    }

    [Fact]
    public void TryLift_blaze_style_loop_pose_adds_pose_loop_unsupported_warning()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(BlazeLoopPoseSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var pose = roots[0]!["children"]!.AsArray()[0]!["pose"]!.AsObject();
        var warnings = pose["liftWarnings"]!.AsArray().Select(n => (string?)n).ToArray();
        Assert.Contains("pose_loop_unsupported", warnings);
    }
}
