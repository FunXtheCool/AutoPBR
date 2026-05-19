using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class JavapMeshExtractionTests
{
    /// <summary>Minimal javap slice: static MeshDefinition factory with parameters and addBox(..., CubeDeformation).</summary>
    private const string BabyHorseCreateBabyMeshJavapSlice = """
public class net.minecraft.client.model.animal.equine.BabyHorseModel {
  public static net.minecraft.client.model.geom.builders.MeshDefinition createBabyMesh(net.minecraft.client.model.geom.builders.CubeDeformation);
    Code:
      13: aload_2
      14: ldc           #18                 // String body
      16: invokestatic  #20                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      19: iconst_0
      20: bipush        13
      22: invokevirtual #26                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      25: ldc           #30                 // float -4.0f
      27: ldc           #31                 // float -3.5f
      29: ldc           #32                 // float -7.0f
      31: ldc           #33                 // float 8.0f
      33: ldc           #34                 // float 7.0f
      35: ldc           #35                 // float 14.0f
      37: aload_0
      38: invokevirtual #36                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      41: fconst_0
      42: ldc           #40                 // float 12.5f
      44: fconst_0
      45: invokestatic  #41                 // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      48: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      51: astore_3
    }
}
""";

    [Fact]
    public void ConcatMeshFactoryCode_Includes_parameterized_static_mesh_methods()
    {
        var concat = JavapClassDisassembly.ConcatMeshFactoryCode(BabyHorseCreateBabyMeshJavapSlice);
        Assert.Contains("addBox", concat, StringComparison.Ordinal);
        Assert.Contains("texOffs:(II)", concat, StringComparison.Ordinal);
        Assert.Contains("CubeDeformation", concat, StringComparison.Ordinal);
    }

    /// <summary>javac often loads the part id ldc <b>after</b> the mesh builder chain, immediately before addOrReplaceChild.</summary>
    private const string PartNameAfterMeshJavapSlice = """
public class net.minecraft.client.model.object.projectile.ArrowModel {
  public static net.minecraft.client.model.geom.builders.LayerDefinition createBodyLayer();
    Code:
      10: invokestatic  #10                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      13: iconst_0
      14: bipush        16
      16: invokevirtual #12                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      19: ldc           #20                 // float -12.0f
      21: ldc           #21                 // float -2.5f
      23: ldc           #22                 // float -2.5f
      25: ldc           #23                 // float 0.0f
      27: ldc           #24                 // float 2.5f
      29: ldc           #25                 // float 2.5f
      31: invokevirtual #26                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      34: ldc           #30                 // float -11.0f
      36: fconst_0
      37: fconst_0
      38: ldc           #31                 // float 0.7853982f
      40: fconst_0
      41: fconst_0
      42: invokestatic  #32                 // Method net/minecraft/client/model/geom/PartPose.offsetAndRotation:(FFFFFF)Lnet/minecraft/client/model/geom/PartPose;
      45: ldc           #40                 // String back
      47: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      50: astore_3
    }
}
""";

    [Fact]
    public void TryLift_Parses_addBox_with_cube_deformation_overload()
    {
        var mesh = JavapClassDisassembly.ConcatMeshFactoryCode(BabyHorseCreateBabyMeshJavapSlice);
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        Assert.NotNull(roots);
        Assert.Single(roots);
        var root = roots[0]!.AsObject();
        Assert.Equal("root", (string?)root["id"]);
        var children = root["children"]!.AsArray();
        Assert.NotEmpty(children);
        var body = children[0]!.AsObject();
        Assert.Equal("body", (string?)body["id"]);
        var cuboids = body["cuboids"]!.AsArray();
        Assert.Single(cuboids);
        var c0 = cuboids[0]!.AsObject();
        Assert.Equal(-4d, c0["from"]![0]!.GetValue<double>());
        Assert.Equal(4d, c0["to"]![0]!.GetValue<double>());
        Assert.Equal(0, c0["uvOrigin"]![0]!.GetValue<int>());
        Assert.Equal(13, c0["uvOrigin"]![1]!.GetValue<int>());
    }

    [Fact]
    public void TryLift_Resolves_part_id_from_ldc_before_addOrReplaceChild_when_none_before_create()
    {
        var mesh = JavapClassDisassembly.ConcatMeshFactoryCode(PartNameAfterMeshJavapSlice);
        Assert.True(JavapFloatGeometryMeshLift.TryLift(mesh, out var roots, out var notes), string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var child = root["children"]!.AsArray()[0]!.AsObject();
        Assert.Equal("back", (string?)child["id"]);
        Assert.Single(child["cuboids"]!.AsArray());
    }

    /// <summary>Obfuscated <c>javap -c</c> slice (1.21.x style) matching fluent <c>hdl</c> builder + <c>hdq</c> part def.</summary>
    private const string ObfuscatedCowMeshSlice = """
public class hak {
  static hdo f();
    Code:
      14: ldc           #42                 // String head
      16: invokestatic  #48                 // Method hdl.c:()Lhdl;
      19: iconst_0
      20: iconst_0
      21: invokevirtual #51                 // Method hdl.a:(II)Lhdl;
      24: ldc           #52                 // float -4.0f
      26: ldc           #52                 // float -4.0f
      28: ldc           #53                 // float -6.0f
      30: ldc           #54                 // float 8.0f
      32: ldc           #54                 // float 8.0f
      34: ldc           #55                 // float 6.0f
      36: invokevirtual #58                 // Method hdl.a:(FFFFFF)Lhdl;
      39: iconst_1
      40: bipush        33
      42: invokevirtual #51                 // Method hdl.a:(II)Lhdl;
      45: ldc           #59                 // float -3.0f
      47: fconst_1
      48: ldc           #60                 // float -7.0f
      50: ldc           #55                 // float 6.0f
      52: ldc           #61                 // float 3.0f
      54: fconst_1
      55: invokevirtual #58                 // Method hdl.a:(FFFFFF)Lhdl;
     100: fconst_0
     101: ldc           #70                 // float 4.0f
     103: ldc           #71                 // float -8.0f
     105: invokestatic  #76                 // Method hdi.a:(FFF)Lhdi;
     108: invokevirtual #81                 // Method hdq.a:(Ljava/lang/String;Lhdl;Lhdi;)Lhdq;
     111: pop
    }
}
""";

    [Fact]
    public void TryLift_Parses_obfuscated_fluent_addBox_and_binding_line()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(ObfuscatedCowMeshSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var root = roots[0]!.AsObject();
        var child = root["children"]!.AsArray()[0]!.AsObject();
        Assert.Equal("head", (string?)child["id"]);
        Assert.Equal(2, child["cuboids"]!.AsArray().Count);
    }
}
