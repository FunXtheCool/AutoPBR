

namespace AutoPBR.Core.Tests;

/// <summary>
/// Post–26.1 profiles: several humanoids reuse the adult <c>HumanoidModel</c> mesh with uniform <c>LivingEntity.getAgeScale()</c>
/// (<c>DEFAULT_BABY_SCALE = 0.5F</c>) instead of an anisotropic baby scale profile tuple.
/// </summary>
public sealed class PostBabyUniformHumanoidMeshParityTests
{
    private static readonly MinecraftNativeProfile Profile2612 = new("26.1.2", "unused", new Version(26, 1, 2));

    private static bool HasElementSize(MergedJavaBlockModel model, float x, float y, float z, float epsilon = 0.3f)
    {
        foreach (var e in model.Elements)
        {
            var dx = MathF.Abs(e.To[0] - e.From[0]);
            var dy = MathF.Abs(e.To[1] - e.From[1]);
            var dz = MathF.Abs(e.To[2] - e.From[2]);
            if (MathF.Abs(dx - x) <= epsilon &&
                MathF.Abs(dy - y) <= epsilon &&
                MathF.Abs(dz - z) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void SkeletonFamily_BabyOn2612_ScalesTorsoToHalfOfAdult()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/skeleton/skeleton.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/skeleton/skeleton_baby.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var baby));
        Assert.True(HasElementSize(adult, 8f, 12f, 4f, epsilon: 0.15f));
        Assert.True(HasElementSize(baby, 4f, 6f, 2f, epsilon: 0.15f));
        Assert.False(HasElementSize(baby, 8f, 12f, 4f, epsilon: 0.15f));
    }

    [Fact]
    public void PlayerWide_BabyOn2612_ScalesTorsoToHalfOfAdult()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/player/wide/steve.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/player/wide/steve_baby.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var baby));
        Assert.True(HasElementSize(adult, 8f, 12f, 4f, epsilon: 0.15f));
        Assert.True(HasElementSize(baby, 4f, 6f, 2f, epsilon: 0.15f));
        Assert.False(HasElementSize(baby, 8f, 12f, 4f, epsilon: 0.15f));
    }

    [Fact]
    public void Pillager_BabyOn2612_ScalesMainRobeBoxToHalfOfAdult()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/illager/pillager.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/illager/pillager_baby.png",
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var baby));
        Assert.True(HasElementSize(adult, 8f, 12f, 6f, epsilon: 0.15f));
        Assert.True(HasElementSize(baby, 4f, 6f, 3f, epsilon: 0.15f));
        Assert.False(HasElementSize(baby, 8f, 12f, 6f, epsilon: 0.15f));
    }
}
