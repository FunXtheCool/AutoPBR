using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class HumanoidPreviewPoseTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string PlayerWidePath = "assets/minecraft/textures/entity/player/wide/steve.png";
    private const string ZombieVillagerPath =
        "assets/minecraft/textures/entity/zombie_villager/profession/leatherworker.png";

    [Fact]
    public void TryGetPoseOptions_returns_humanoid_poses_for_player_wide()
    {
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(PlayerWidePath, "PlayerWide", out var options));
        Assert.Equal(7, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.HumanoidEmpty);
    }

    [Fact]
    public void TryGetPoseOptions_includes_zombie_arms_default_for_zombie_villager()
    {
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(
            ZombieVillagerPath,
            "HumanoidZombieVillager",
            out var options));
        Assert.Equal(8, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.HumanoidZombieArms);
    }

    [Theory]
    [InlineData("PlayerWide", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("PlayerSlim", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("HumanoidGeneric", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("HumanoidZombieVillager", EntityHumanoidPreviewArmPose.ZombieArms)]
    public void ResolveEffectiveHumanoidArmPose_uses_builder_default_when_no_selector(
        string builderMethod,
        EntityHumanoidPreviewArmPose expected)
    {
        var pose = EntityPreviewPoseCatalog.ResolveEffectiveHumanoidArmPose(builderMethod, selectedPoseId: null);
        Assert.Equal(expected, pose);
    }

    [Fact]
    public void Player_bow_and_empty_selector_poses_produce_different_static_meshes()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel empty;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PlayerWidePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out empty,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotEmpty(empty.Elements);
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidBowAndArrow))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PlayerWidePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var bow,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(empty.Elements.Count, bow.Elements.Count);
            var maxErr = MaxMatrixDelta(empty.Elements[3].LocalToParent, bow.Elements[3].LocalToParent);
            Assert.True(maxErr > 0.05f, $"expected arm pose delta, got {maxErr:F4}");
        }
    }

    [Fact]
    public void Zombie_villager_zombie_arms_and_empty_poses_produce_different_static_meshes()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel zombieArms;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                ZombieVillagerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out zombieArms,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotEmpty(zombieArms.Elements);
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                ZombieVillagerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var empty,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            var maxErr = 0f;
            for (var i = 0; i < zombieArms.Elements.Count; i++)
            {
                maxErr = MathF.Max(
                    maxErr,
                    MaxMatrixDelta(zombieArms.Elements[i].LocalToParent, empty.Elements[i].LocalToParent));
            }

            Assert.True(maxErr > 0.05f, $"expected arm pose delta, got {maxErr:F4}");
        }
    }

    private static float MaxMatrixDelta(System.Numerics.Matrix4x4 a, System.Numerics.Matrix4x4 b)
    {
        var max = 0f;
        max = MathF.Max(max, MathF.Abs(a.M11 - b.M11));
        max = MathF.Max(max, MathF.Abs(a.M12 - b.M12));
        max = MathF.Max(max, MathF.Abs(a.M13 - b.M13));
        max = MathF.Max(max, MathF.Abs(a.M14 - b.M14));
        max = MathF.Max(max, MathF.Abs(a.M21 - b.M21));
        max = MathF.Max(max, MathF.Abs(a.M22 - b.M22));
        max = MathF.Max(max, MathF.Abs(a.M23 - b.M23));
        max = MathF.Max(max, MathF.Abs(a.M24 - b.M24));
        max = MathF.Max(max, MathF.Abs(a.M31 - b.M31));
        max = MathF.Max(max, MathF.Abs(a.M32 - b.M32));
        max = MathF.Max(max, MathF.Abs(a.M33 - b.M33));
        max = MathF.Max(max, MathF.Abs(a.M34 - b.M34));
        max = MathF.Max(max, MathF.Abs(a.M41 - b.M41));
        max = MathF.Max(max, MathF.Abs(a.M42 - b.M42));
        max = MathF.Max(max, MathF.Abs(a.M43 - b.M43));
        max = MathF.Max(max, MathF.Abs(a.M44 - b.M44));
        return max;
    }
}
