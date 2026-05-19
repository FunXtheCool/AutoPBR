using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class QuadrupedLegScalePrecedenceTests
{
    [Fact]
    public void BuildBoxIntLocals_createLegs_scale_wins_over_createBodyMesh_regardless_of_concat_order()
    {
        var legsFirst = new[]
        {
            "      10: bipush        8",
            "      12: invokestatic  #123 // Method createLegs:(Lnet/minecraft/client/model/geom/builders/PartDefinition;ZZILnet/minecraft/client/model/geom/builders/CubeDeformation;)V",
            "      15: bipush       12",
            "      17: invokestatic  #124 // Method createBodyMesh:(IZZLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/MeshDefinition;",
        };
        var bodyFirst = new[]
        {
            "      10: bipush       12",
            "      12: invokestatic  #124 // Method createBodyMesh:(IZZLnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/MeshDefinition;",
            "      15: bipush        8",
            "      17: invokestatic  #123 // Method createLegs:(Lnet/minecraft/client/model/geom/builders/PartDefinition;ZZILnet/minecraft/client/model/geom/builders/CubeDeformation;)V",
        };

        var legsFirstMap = JavapFloatGeometryMeshLift.BuildBoxIntLocalConstantsForTests(legsFirst);
        var bodyFirstMap = JavapFloatGeometryMeshLift.BuildBoxIntLocalConstantsForTests(bodyFirst);

        Assert.Equal(8, legsFirstMap[0]);
        Assert.Equal(8, legsFirstMap[3]);
        Assert.Equal(8, bodyFirstMap[0]);
        Assert.Equal(8, bodyFirstMap[3]);
    }
}
