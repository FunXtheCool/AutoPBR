using System.Text.Json.Nodes;



namespace AutoPBR.GeometryCompiler.Tests;

public sealed class CubeDeformationInflateLiftTests
{
    /// <summary>
    /// <c>HumanoidModel</c>-style <c>aload_*</c> deformation ref before <c>addBox</c>; inline ctor not emitted, no <c>inflate</c> property.
    /// </summary>
    private const string AloadCubeDefAddBoxSlice = """
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

    /// <summary>
    /// Same as <see cref="AloadCubeDefAddBoxSlice"/> but <c>new CubeDeformation(f)</c> inline before <c>addBox</c>.</summary>
    private const string InlineCubeDefAddBoxSlice = """
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
     204: new           #90                 // class net/minecraft/client/model/geom/builders/CubeDeformation
     207: dup
     208: ldc           #201                // float 0.6875f
     210: invokespecial #92                 // Method net/minecraft/client/model/geom/builders/CubeDeformation."<init>":(F)V
     213: invokevirtual #79                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     216: ldc           #115                // float 5.0f
     218: fconst_2
     219: fload_1
     220: fadd
     221: fconst_0
     222: invokestatic  #83                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
     225: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary><c>new CubeDeformation(f)</c> stored to a reference local (<c>astore_*</c>) then <c>aload_*</c> before <c>addBox</c> (humanoid delegation style).</summary>
    private const string AloadStoredCubeDefInflateSlice = """
    Code:
       4: ldc           #201                // float 0.3125f
       6: new           #90                 // class net/minecraft/client/model/geom/builders/CubeDeformation
       9: dup
      10: ldc           #201                // float 0.3125f
      12: invokespecial #92                 // Method net/minecraft/client/model/geom/builders/CubeDeformation."<init>":(F)V
      15: astore_2
      16: aload_3
      17: ldc           #42                 // String left_arm
      19: invokestatic  #66                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      22: bipush        40
      24: bipush        16
      26: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      29: invokevirtual #111                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.mirror:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      32: ldc           #114                // float -1.0f
      34: ldc           #106                // float -2.0f
      36: ldc           #106                // float -2.0f
      38: ldc           #108                // float 4.0f
      40: ldc           #107                // float 12.0f
      42: ldc           #108                // float 4.0f
      44: aload_2
      45: invokevirtual #79                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      48: ldc           #115                // float 5.0f
      50: fconst_2
      51: fload_1
      52: fadd
      53: fconst_0
      54: invokestatic  #83                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      57: invokevirtual #89                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void TryLift_Omits_inflate_when_CubeDeformation_is_aload_mesh_param()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(AloadCubeDefAddBoxSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = CuboidZero(roots, "left_arm");
        Assert.False(cub.ContainsKey("inflate"));
    }

    [Fact]
    public void TryLift_emits_inflate_when_CubeDeformation_ctor_is_inline_const_F()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(InlineCubeDefAddBoxSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = CuboidZero(roots, "left_arm");
        Assert.True(cub.ContainsKey("inflate"));
        Assert.Equal(0.6875, cub["inflate"]!.GetValue<double>(), 8);
    }

    [Fact]
    public void TryLift_emits_inflate_when_CubeDeformation_ctor_is_astore_then_aload_ref()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(AloadStoredCubeDefInflateSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = CuboidZero(roots, "left_arm");
        Assert.True(cub.ContainsKey("inflate"));
        Assert.Equal(0.3125, cub["inflate"]!.GetValue<double>(), 8);
    }

    private static JsonObject CuboidZero(JsonArray roots, string partId)
    {
        var root = roots[0]!.AsObject();
        var part = root["children"]!.AsArray().OfType<JsonObject>()
            .First(j => string.Equals((string?)j["id"], partId, StringComparison.Ordinal));
        return part["cuboids"]!.AsArray()[0]!.AsObject();
    }
}
