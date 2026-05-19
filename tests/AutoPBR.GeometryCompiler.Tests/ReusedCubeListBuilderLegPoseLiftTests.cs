using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Quadruped <c>createLegs</c> reuses one <c>CubeListBuilder</c> astore; each <c>ldc</c> + <c>PartPose</c> bind must lift its own offset.
/// </summary>
public sealed class ReusedCubeListBuilderLegPoseLiftTests
{
    private const string ReusedLegTemplateAndTwoBinds = """
        Code:
           0: invokestatic  #1                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
           3: iconst_0
           4: bipush        16
           6: invokevirtual #2                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
           9: ldc           #3                 // float -2.0f
          11: fconst_0
          12: ldc           #3                 // float -2.0f
          14: ldc           #4                 // float 4.0f
          16: ldc           #5                 // float 12.0f
          18: ldc           #4                 // float 4.0f
          20: getstatic     #6                 // Field net/minecraft/client/model/geom/builders/CubeDeformation.NONE:Lnet/minecraft/client/model/geom/builders/CubeDeformation;
          23: invokevirtual #7                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
          26: astore_2
          27: ldc           #8                 // String right_hind_leg
          29: aload_2
          30: ldc           #9                 // float -4.0f
          32: ldc           #10                // float 12.0f
          34: ldc           #11                // float 7.0f
          36: invokestatic  #12                // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
          39: invokevirtual #13                // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
          42: ldc           #14                // String left_hind_leg
          44: aload_2
          45: ldc           #4                 // float 4.0f
          47: ldc           #10                // float 12.0f
          49: ldc           #11                // float 7.0f
          51: invokestatic  #12                // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
          54: invokevirtual #13                // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
        """;

    [Fact]
    public void Reused_builder_bindings_lift_distinct_leg_part_pose_offsets()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(ReusedLegTemplateAndTwoBinds, out var roots, out var notes),
            string.Join("; ", notes));

        var children = roots[0]!["children"]!.AsArray().OfType<JsonObject>().ToDictionary(o => (string)o["id"]!);
        Assert.True(children.TryGetValue("right_hind_leg", out var right));
        Assert.True(children.TryGetValue("left_hind_leg", out var left));

        static double Tx(JsonObject part) => part["pose"]!["translation"]![0]!.GetValue<double>();

        Assert.Equal(-4d, Tx(right), 0.01);
        Assert.Equal(4d, Tx(left), 0.01);
        Assert.NotNull(right["pose"]!["setupAnimPivot"]);
        Assert.NotNull(left["pose"]!["setupAnimPivot"]);
    }

    [Fact]
    public void Cow_style_mirror_noarg_reused_leg_template_lifts()
    {
        const string cowLeg = """
            Code:
               0: invokestatic  #32                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
               3: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.mirror:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
               6: iconst_0
               7: bipush        16
               9: invokevirtual #37                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
              12: ldc           #80                 // float -2.0f
              14: fconst_0
              15: ldc           #80                 // float -2.0f
              17: ldc           #60                 // float 4.0f
              19: ldc           #77                 // float 12.0f
              21: ldc           #60                 // float 4.0f
              23: invokevirtual #45                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
              26: astore_3
              27: invokestatic  #32                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
              30: iconst_0
              31: bipush        16
              33: invokevirtual #37                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
              36: ldc           #80                 // float -2.0f
              38: fconst_0
              39: ldc           #80                 // float -2.0f
              41: ldc           #60                 // float 4.0f
              43: ldc           #77                 // float 12.0f
              45: ldc           #60                 // float 4.0f
              47: invokevirtual #45                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
              50: astore_2
              51: aload_1
              52: ldc           #92                 // String right_hind_leg
              54: aload_3
              55: ldc           #41                 // float -4.0f
              57: ldc           #77                 // float 12.0f
              59: ldc           #94                 // float 7.0f
              61: invokestatic  #62                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
              64: invokevirtual #68                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
            """;
        Assert.True(JavapFloatGeometryMeshLift.TryLift(cowLeg, out var roots, out var notes), string.Join("; ", notes));
        var ids = roots[0]!["children"]!.AsArray().OfType<JsonObject>().Select(o => (string)o["id"]!).ToList();
        Assert.Contains("right_hind_leg", ids);
    }
}
