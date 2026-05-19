using System.Text.Json.Nodes;


namespace AutoPBR.GeometryCompiler.Tests;

public sealed class PiglinAddHeadNestedPartLiftTests
{
    /// <summary>Subset of <c>AbstractPiglinModel.addHead</c> javap from 26.1.2 named client.jar.</summary>
    private const string AddHeadCodeSlice = """
    Code:
       0: aload_1
       1: invokevirtual #48                 // Method net/minecraft/client/model/geom/builders/MeshDefinition.getRoot:()Lnet/minecraft/client/model/geom/builders/PartDefinition;
       4: astore_2
       5: aload_2
       6: ldc           #54                 // String head
       8: invokestatic  #60                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      11: iconst_0
      12: iconst_0
      13: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      16: ldc           #80                 // float -5.0f
      18: ldc           #81                 // float -8.0f
      20: ldc           #82                 // float -4.0f
      22: ldc           #83                 // float 10.0f
      24: ldc           #84                 // float 8.0f
      26: ldc           #84                 // float 8.0f
      28: aload_0
      29: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      32: bipush        31
      34: iconst_1
      35: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      38: ldc           #89                 // float -2.0f
      40: ldc           #82                 // float -4.0f
      42: ldc           #80                 // float -5.0f
      44: ldc           #90                 // float 4.0f
      46: ldc           #90                 // float 4.0f
      48: fconst_1
      49: aload_0
      50: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      53: iconst_2
      54: iconst_4
      55: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      58: fconst_2
      59: ldc           #89                 // float -2.0f
      61: ldc           #80                 // float -5.0f
      63: fconst_1
      64: fconst_2
      65: fconst_1
      66: aload_0
      67: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      70: iconst_2
      71: iconst_0
      72: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      75: ldc           #91                 // float -3.0f
      77: ldc           #89                 // float -2.0f
      79: ldc           #80                 // float -5.0f
      81: fconst_1
      82: fconst_2
      83: fconst_1
      84: aload_0
      85: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      88: getstatic     #66                 // Field net/minecraft/client/model/geom/PartPose.ZERO:Lnet/minecraft/client/model/geom/PartPose;
      91: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      94: astore_3
      95: aload_3
      96: ldc           #30                 // String left_ear
      98: invokestatic  #60                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     101: bipush        51
     103: bipush        6
     105: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     108: fconst_0
     109: fconst_0
     110: ldc           #89                 // float -2.0f
     112: fconst_1
     113: ldc           #92                 // float 5.0f
     115: ldc           #90                 // float 4.0f
     117: aload_0
     118: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     121: ldc           #93                 // float 4.5f
     123: ldc           #94                 // float -6.0f
     125: fconst_0
     126: fconst_0
     127: fconst_0
     128: ldc           #97                 // float -0.5235988f
     130: invokestatic  #98                 // Method net/minecraft/client/model/geom/PartPose.offsetAndRotation:(FFFFFF)Lnet/minecraft/client/model/geom/PartPose;
     133: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
     136: pop
     137: aload_3
     138: ldc           #19                 // String right_ear
     140: invokestatic  #60                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     143: bipush        39
     145: bipush        6
     147: invokevirtual #76                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     150: ldc           #102                // float -1.0f
     152: fconst_0
     153: ldc           #89                 // float -2.0f
     155: fconst_1
     156: ldc           #92                 // float 5.0f
     158: ldc           #90                 // float 4.0f
     160: aload_0
     161: invokevirtual #85                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
     164: ldc           #103                // float -4.5f
     166: ldc           #94                 // float -6.0f
     168: fconst_0
     169: fconst_0
     170: fconst_0
     171: ldc           #104                // float 0.5235988f
     173: invokestatic  #98                 // Method net/minecraft/client/model/geom/PartPose.offsetAndRotation:(FFFFFF)Lnet/minecraft/client/model/geom/PartPose;
     176: invokevirtual #72                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
     179: pop
    """;

    [Fact]
    public void TryLift_Nests_ears_under_head_for_abstract_piglin_addHead_slice()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(AddHeadCodeSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var children = root["children"]!.AsArray();
        var head = FirstChildPart(children, "head");
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "left_ear");
        AssertChildPartId(headKids, "right_ear");
    }

    private static JsonObject FirstChildPart(JsonArray children, string id) =>
        children.OfType<JsonObject>().First(j => string.Equals((string?)j["id"], id, StringComparison.Ordinal));

    private static void AssertChildPartId(JsonArray parts, string id) =>
        Assert.Contains(parts, n => n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal));
}
