using System.Text.Json.Nodes;



namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Regression: nested <c>PartDefinition.addOrReplaceChild</c> (head → beak) must lift for non-piglin models too.
/// </summary>
public sealed class ChickenNestedPartLiftTests
{
    /// <summary>Subset of <c>AdultChickenModel.createBaseChickenModel</c> javap from 26.1.2 named client.jar.</summary>
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

    private const string CreateBodyLayerOnlySlice = """
    Code:
       0: invokestatic  #40                 // Method createBaseChickenModel:()Lnet/minecraft/client/model/geom/builders/MeshDefinition;
       3: astore_0
       4: aload_0
       5: bipush        64
       7: bipush        32
       9: invokestatic  #44                 // Method net/minecraft/client/model/geom/builders/LayerDefinition.create:(Lnet/minecraft/client/model/geom/builders/MeshDefinition;II)Lnet/minecraft/client/model/geom/builders/LayerDefinition;
      12: areturn
    """;

    [Fact]
    public void TryLift_Nests_beak_when_createBodyLayer_precedes_createBase_without_island_marker()
    {
        var mesh = CreateBodyLayerOnlySlice + "\n" + CreateBaseChickenModelCodeSlice;
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = FirstChildPart(children, "head");
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "beak");
    }

    [Fact]
    public void TryLift_Nests_beak_under_head_for_chicken_createBase_slice()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(CreateBaseChickenModelCodeSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = FirstChildPart(children, "head");
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "beak");
    }

    [Fact]
    public void TryLift_Nests_beak_when_createBodyLayer_is_separated_from_createBase_by_mesh_island_marker()
    {
        var mesh = CreateBodyLayerOnlySlice + "\n" + JavapClassDisassembly.GeometryMeshIslandBoundaryMarker + "\n" +
                   CreateBaseChickenModelCodeSlice;
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = FirstChildPart(children, "head");
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "beak");
    }

    [Fact]
    public void TryLift_Nests_beak_when_prefixed_with_mesh_island_marker_like_ConcatMeshFactoryCodeDeep()
    {
        var mesh = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker + "\n" + CreateBaseChickenModelCodeSlice;
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = FirstChildPart(children, "head");
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "beak");
    }

    private static JsonObject FirstChildPart(JsonArray children, string id) =>
        children.OfType<JsonObject>().First(j => string.Equals((string?)j["id"], id, StringComparison.Ordinal));

    private static void AssertChildPartId(JsonArray parts, string id) =>
        Assert.Contains(parts, n => n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal));

    [Fact]
    public void TryLift_beak_segment_keeps_addBox_when_parent_slot_was_astore_before_aload()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(CreateBaseChickenModelCodeSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var head = FirstChildPart(roots[0]!["children"]!.AsArray(), "head");
        var beak = FirstChildPart(head["children"]!.AsArray(), "beak");
        var cuboids = beak["cuboids"]!.AsArray();
        Assert.Single(cuboids);
        Assert.Equal(-2, cuboids[0]!["from"]![0]!.GetValue<double>());
    }
}
