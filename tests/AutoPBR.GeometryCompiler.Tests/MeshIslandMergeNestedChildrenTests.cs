using System.Text.Json.Nodes;



namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Regression: concatenated mesh islands merge root parts last-wins while preserving nested children only defined on earlier islands.
/// </summary>
public sealed class MeshIslandMergeNestedChildrenTests
{
    /// <summary>Subset of <c>AdultChickenModel.createBaseChickenModel</c> (head + nested beak).</summary>
    private const string CreateBaseChickenModelCodeSlice = """
    Code:
       8: aload_0
       9: invokevirtual #55                 // Method net/minecraft/client/model/geom/builders/MeshDefinition.getRoot:()Lnet/minecraft/client/model/geom/builders/PartDefinition;
      12: astore_1
      13: aload_1
      14: ldc           #9                  // String head
      16: invokestatic  #59                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      19: iconst_0
      20: iconst_0
      21: invokevirtual #64                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      24: ldc           #68                 // float -2.0f
      26: ldc           #69                 // float -6.0f
      28: ldc           #68                 // float -2.0f
      30: ldc           #70                 // float 4.0f
      32: ldc           #71                 // float 6.0f
      34: ldc           #72                 // float 3.0f
      36: invokevirtual #73                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      39: fconst_0
      40: ldc           #77                 // float 15.0f
      42: ldc           #78                 // float -4.0f
      44: invokestatic  #79                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      47: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      50: astore_2
      51: aload_2
      52: ldc           #91                 // String beak
      54: invokestatic  #59                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      57: bipush        14
      59: iconst_0
      60: invokevirtual #64                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      63: ldc           #68                 // float -2.0f
      65: ldc           #78                 // float -4.0f
      67: ldc           #78                 // float -4.0f
      69: ldc           #70                 // float 4.0f
      71: fconst_2
      72: fconst_2
      73: invokevirtual #73                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      76: getstatic     #93                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      79: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/PartPose;
      82: pop
    """;

    /// <summary>Later island refreshes root <c>head</c> mesh only (no <c>beak</c> child).</summary>
    private const string HeadSecondIslandSingleCuboidSlice = """
    Code:
       8: aload_0
       9: invokevirtual #55                 // Method net/minecraft/client/model/geom/builders/MeshDefinition.getRoot:()Lnet/minecraft/client/model/geom/builders/PartDefinition;
      12: astore_1
      13: aload_1
      14: ldc           #9                  // String head
      16: invokestatic  #59                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      19: iconst_1
      20: iconst_2
      21: invokevirtual #64                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      24: fconst_0
      25: fconst_0
      26: fconst_0
      27: fconst_1
      28: fconst_1
      29: fconst_1
      30: invokevirtual #73                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      33: fconst_0
      34: fconst_0
      35: fconst_0
      36: invokestatic  #79                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      39: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      42: pop
    """;

    [Fact]
    public void TryLift_second_island_replaces_head_cuboids_but_keeps_nested_beak_child()
    {
        var mesh = CreateBaseChickenModelCodeSlice + "\n" + JavapClassDisassembly.GeometryMeshIslandBoundaryMarker + "\n" +
                   HeadSecondIslandSingleCuboidSlice;
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = children.OfType<JsonObject>().First(j => string.Equals((string?)j["id"], "head", StringComparison.Ordinal));
        Assert.Single(head["cuboids"]!.AsArray());
        var uv = head["cuboids"]![0]!["uvOrigin"]!.AsArray();
        Assert.Equal(1, uv[0]!.GetValue<int>());
        Assert.Equal(2, uv[1]!.GetValue<int>());
        var headKids = head["children"]!.AsArray();
        Assert.Contains(headKids, n => n is JsonObject j && string.Equals((string?)j["id"], "beak", StringComparison.Ordinal));
    }
}
