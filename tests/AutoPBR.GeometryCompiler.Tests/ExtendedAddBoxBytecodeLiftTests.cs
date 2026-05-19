


namespace AutoPBR.GeometryCompiler.Tests;

public sealed class ExtendedAddBoxBytecodeLiftTests
{
    private static readonly string[] NorthSouthDirectionMask = ["north", "south"];
    /// <summary><c>addBox(Ljava/lang/String;FFFFFF)</c>: quad ldc with floats; part id ldc before <c>create</c>.</summary>
    private const string StringMirrorQuadFloat6Slice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #50                 // String fin_back
      20: ldc           #30                 // float -2.0f
      22: fconst_0
      23: ldc           #30                 // float -2.0f
      25: ldc           #31                 // float 4.0f
      27: ldc           #32                 // float 12.0f
      29: ldc           #31                 // float 4.0f
      31: invokevirtual #16                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(Ljava/lang/String;FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      34: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      37: ldc           #8                  // String body
      39: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary><c>addBox(Ljava/lang/String;FFFIIIII)</c> texCrop-style without deformation.</summary>
    private const string TexCropNoDefSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #60                 // String body_part
      20: ldc           #50                 // float 1.0f
      22: ldc           #51                 // float 2.0f
      24: ldc           #52                 // float 3.0f
      26: iconst_4
      27: iconst_5
      28: bipush        6
      30: iconst_2
      31: iconst_3
      32: invokevirtual #70                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(Ljava/lang/String;FFFIIIII)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      35: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      38: ldc           #8                  // String body
      40: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary><c>addBox(String,FFFIII, CubeDeformation, II)</c> with <c>CubeDeformation.NONE</c> static ref.</summary>
    private const string TexCropWithCubeDeformationStaticSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #60                 // String fin_crop
      20: fconst_0
      21: fconst_0
      22: fconst_0
      23: iconst_2
      24: iconst_3
      25: iconst_4
      26: getstatic     #88                 // Field net/minecraft/client/model/geom/builders/CubeDeformation.NONE:Lnet/minecraft/client/model/geom/builders/CubeDeformation;
      27: iconst_5
      28: bipush        7
      29: invokevirtual #90                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(Ljava/lang/String;FFFIIILnet/minecraft/client/model/geom/builders/CubeDeformation;II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      32: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      35: ldc           #8                  // String body
      37: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary><c>addBox(Ljava/lang/String;FFFFFFLjava/util/Set;)</c> quad key + direction mask.</summary>
    private const string StringQuadDirectionMaskSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #55                 // String wing_edge
      20: ldc           #30                 // float -1.0f
      22: ldc           #31                 // float -2.0f
      24: ldc           #32                 // float -3.0f
      26: ldc           #33                 // float 4.0f
      28: ldc           #34                 // float 5.0f
      30: ldc           #35                 // float 6.0f
      32: invokestatic  #80                 // Method java/util/Collections.emptySet:()Ljava/util/Set;
      35: invokevirtual #91                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(Ljava/lang/String;FFFFFFLjava/util/Set;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      38: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      41: ldc           #8                  // String body
      43: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    /// <summary>Six floats then empty set + direction-mask overload.</summary>
    private const string DirectionMaskFloat6Slice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #30                 // float -1.0f
      20: ldc           #31                 // float -2.0f
      22: ldc           #32                 // float -3.0f
      24: ldc           #33                 // float 4.0f
      26: ldc           #34                 // float 5.0f
      28: ldc           #35                 // float 6.0f
      30: invokestatic  #80                 // Method java/util/Collections.emptySet:()Ljava/util/Set;
      33: invokevirtual #81                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLjava/util/Set;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      36: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      39: ldc           #8                  // String shell
      41: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void TryLift_maps_string_mirror_overload_ldc_to_textureKey()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(StringMirrorQuadFloat6Slice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("#fin_back", (string?)cub["textureKey"]);
        Assert.Equal("body", (string?)roots[0]!["children"]!.AsArray()[0]!["id"]);
    }

    [Fact]
    public void TryLift_tex_crop_no_def_emits_uv_span_and_geometry_from_int_dims()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(TexCropNoDefSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("#body_part", (string?)cub["textureKey"]);
        var span = cub["uvSpan"]!.AsArray();
        Assert.Equal(2, span[0]!.GetValue<int>());
        Assert.Equal(3, span[1]!.GetValue<int>());
        Assert.Equal(1d, cub["from"]![0]!.GetValue<double>(), 5);
        Assert.Equal(5d, cub["to"]![0]!.GetValue<double>(), 5);
    }

    [Fact]
    public void TryLift_direction_mask_empty_set_is_exact_with_empty_face_mask()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(DirectionMaskFloat6Slice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("exact", (string?)cub["liftKind"]);
        Assert.Empty(cub["faceMask"]!.AsArray());
    }

    [Fact]
    public void TryLift_tex_crop_with_cube_deformation_static_skips_operand_and_emits_uv_span()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(TexCropWithCubeDeformationStaticSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("#fin_crop", (string?)cub["textureKey"]);
        Assert.Equal(5, cub["uvSpan"]![0]!.GetValue<int>());
        Assert.Equal(7, cub["uvSpan"]![1]!.GetValue<int>());
        Assert.False(cub.ContainsKey("inflate"));
        Assert.Equal(0d, cub["from"]![0]!.GetValue<double>(), 5);
        Assert.Equal(2d, cub["to"]![0]!.GetValue<double>(), 5);
    }

    [Fact]
    public void TryLift_string_quad_direction_mask_sets_texture_key_and_provenance()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(StringQuadDirectionMaskSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("#wing_edge", (string?)cub["textureKey"]);
        Assert.Equal("exact", (string?)cub["liftKind"]);
        Assert.Equal("#wing_edge", (string?)cub["textureKey"]);
    }

    private const string DirectionMaskSetOfSlice = """
    Code:
       8: aload_1
       9: invokestatic  #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      12: iconst_0
      13: bipush        16
      15: invokevirtual #14                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      18: ldc           #30                 // float 0.0f
      20: ldc           #31                 // float 0.0f
      22: ldc           #32                 // float 0.0f
      24: ldc           #33                 // float 4.0f
      26: ldc           #34                 // float 4.0f
      28: ldc           #35                 // float 4.0f
      30: getstatic     #90                 // Field net/minecraft/core/Direction.NORTH:Lnet/minecraft/core/Direction;
      33: getstatic     #91                 // Field net/minecraft/core/Direction.SOUTH:Lnet/minecraft/core/Direction;
      36: invokestatic  #92                 // InterfaceMethod java/util/Set.of:(Ljava/lang/Object;Ljava/lang/Object;)Ljava/util/Set;
      39: invokevirtual #93                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLjava/util/Set;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      42: getstatic     #40                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      45: ldc           #8                  // String mask_part
      47: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void TryLift_set_of_direction_parses_face_mask()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(DirectionMaskSetOfSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("exact", (string?)cub["liftKind"]);
        var mask = cub["faceMask"]!.AsArray().Select(n => (string?)n).OrderBy(s => s).ToArray();
        Assert.Equal(NorthSouthDirectionMask, mask);
    }

    [Fact]
    public void TryLift_tex_crop_emits_tex_crop_static_lift_kind()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(TexCropNoDefSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal("exact", (string?)cub["liftKind"]);
    }
}
