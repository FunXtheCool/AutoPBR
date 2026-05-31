using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ChickenPartPoseIrVersusCleanRoomTests
{
    [Fact]
    public void Bind_pose_model_space_matrices_match_ir_for_root_aligned_parts()
    {
        GeometryIrParityPolicy.ResetForTests();
        var ir = LoadChicken26IrBindPoseMatrices();
        var headPoseIr = ir["head"];
        var bodyPoseIr = ir["body"];
        var beakLeafIr = ir["beak"];

        const string jvm = "net.minecraft.client.model.animal.chicken.ChickenModel";
        var geometryRoot = LoadRepairedGeometryRoot(jvm);
        var merged = CleanRoomEntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "entity/chicken/chicken", jvm, 64, 32, geometryRoot, out _);
        Assert.NotNull(merged);
        Assert.Equal(8, merged!.Elements.Count);

        const float eps = 1e-4f;
        Assert.True(MatricesClose(merged.Elements[0].LocalToParent, headPoseIr, eps), "head slab");
        Assert.True(MatricesClose(merged.Elements[3].LocalToParent, bodyPoseIr, eps), "body");
        Assert.True(MatricesClose(merged.Elements[4].LocalToParent, ir["right_leg"], eps), "right leg");
        Assert.True(MatricesClose(merged.Elements[5].LocalToParent, ir["left_leg"], eps), "left leg");
        Assert.True(MatricesClose(merged.Elements[6].LocalToParent, ir["right_wing"], eps), "right wing");
        Assert.True(MatricesClose(merged.Elements[7].LocalToParent, ir["left_wing"], eps), "left wing");
        Assert.True(MatricesClose(merged.Elements[1].LocalToParent, headPoseIr, eps), "beak");
        Assert.True(MatricesClose(merged.Elements[2].LocalToParent, headPoseIr, eps), "wattle");
        Assert.False(
            MatricesClose(merged.Elements[1].LocalToParent, beakLeafIr, 0.05f),
            "beak IR leaf pose is not the merged model matrix (flat IR vs head-local Java).");
    }

    private static JsonElement LoadRepairedGeometryRoot(string officialJvm)
    {
        var path = ContentPath("docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, doc.RootElement);
    }
}
