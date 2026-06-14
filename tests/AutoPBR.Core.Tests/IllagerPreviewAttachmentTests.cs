using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EntityPreviewPoseCatalogTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/illager/evoker.png", "Evoker", EntityIllagerPreviewArmPose.Spellcasting)]
    [InlineData("assets/minecraft/textures/entity/illager/vindicator.png", "Vindicator", EntityIllagerPreviewArmPose.AttackingWeapon)]
    [InlineData("assets/minecraft/textures/entity/illager/pillager.png", "Pillager", EntityIllagerPreviewArmPose.CrossbowHold)]
    [InlineData("assets/minecraft/textures/entity/illager/illusioner.png", "Illager", EntityIllagerPreviewArmPose.Crossed)]
    public void ResolveEffectiveIllagerArmPose_uses_texture_default_when_no_selector(
        string path,
        string builderMethod,
        EntityIllagerPreviewArmPose expected)
    {
        var pose = EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(path, builderMethod, selectedPoseId: null);
        Assert.Equal(expected, pose);
    }

    [Fact]
    public void TryGetPoseOptions_returns_all_illager_arm_poses_for_evoker()
    {
        const string path = "assets/minecraft/textures/entity/illager/evoker.png";
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(path, "Evoker", out var options));
        Assert.Equal(8, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.IllagerSpellcasting);
    }
}

public sealed class IllagerPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string EvokerPath = "assets/minecraft/textures/entity/illager/evoker.png";
    private const string IllusionerPath = "assets/minecraft/textures/entity/illager/illusioner.png";

    [Fact]
    public void Evoker_default_runtime_mesh_has_no_duplicate_arm_cuboids()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            EvokerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Equal(8, mesh.Elements.Count);
    }

    [Fact]
    public void Illusioner_default_runtime_mesh_uses_folded_arms()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            IllusionerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(9, mesh.Elements.Count);
    }

    [Fact]
    public void Evoker_crossed_selector_pose_switches_to_folded_arms()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerCrossed))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                EvokerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(9, mesh.Elements.Count);
        }
    }
}
