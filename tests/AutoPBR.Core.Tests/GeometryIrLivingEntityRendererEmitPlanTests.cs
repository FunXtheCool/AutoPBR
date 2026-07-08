using System.Numerics;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrLivingEntityRendererEmitPlanTests
{
    private const string CowJvm = "net.minecraft.client.model.animal.cow.CowModel";
    private const string HoglinJvm = "net.minecraft.client.model.monster.hoglin.HoglinModel";

    [Fact]
    public void Static_cow_plan_post_batches_column_pose_stack_root_once()
    {
        var plan = EntityModelRuntime.ResolveGeometryIrParityEmitPlan(
            CowJvm,
            "cow",
            "assets/minecraft/textures/entity/cow/cow_temperate.png",
            deferLivingEntityRendererUntilAfterMotionPasses: false);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, plan.Basis);
        Assert.True(plan.ApplyPostLivingEntityRendererBasis);
        Assert.Equal(Matrix4x4.Identity, plan.EmitRootTransform);
    }

    [Fact]
    public void Static_hoglin_plan_post_batches_column_pose_stack_root_once()
    {
        var plan = EntityModelRuntime.ResolveGeometryIrParityEmitPlan(
            HoglinJvm,
            "hoglin",
            "assets/minecraft/textures/entity/hoglin/hoglin.png",
            deferLivingEntityRendererUntilAfterMotionPasses: false);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, plan.Basis);
        Assert.True(plan.ApplyPostLivingEntityRendererBasis);
        Assert.Equal(Matrix4x4.Identity, plan.EmitRootTransform);
    }

    [Fact]
    public void Animated_catalog_plan_defers_ler_to_single_post_batch()
    {
        var plan = EntityModelRuntime.ResolveGeometryIrParityEmitPlan(
            CowJvm,
            "cow",
            "assets/minecraft/textures/entity/cow/cow_temperate.png",
            deferLivingEntityRendererUntilAfterMotionPasses: true);
        Assert.True(plan.ApplyPostLivingEntityRendererBasis);
        Assert.Equal(Matrix4x4.Identity, plan.EmitRootTransform);
    }
}
