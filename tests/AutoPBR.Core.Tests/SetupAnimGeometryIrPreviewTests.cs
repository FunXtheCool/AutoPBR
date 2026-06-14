using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class SetupAnimGeometryIrPreviewTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Theory]
    [InlineData("assets/minecraft/textures/entity/fox/fox.png")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf.png")]
    public void Geometry_ir_setup_anim_leg_matrices_change_with_walk_clock(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var maxDelta = 0f;
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                Vector3.Distance(
                    Corner(bind.Elements[i].LocalToParent),
                    Corner(walk.Elements[i].LocalToParent)));
        }

        Assert.True(maxDelta > 0.05f, $"expected setupAnim mesh motion for {path} (max delta={maxDelta:F3})");
    }

    [Fact]
    public void Cow_geometry_ir_setup_anim_mesh_changes_with_walk_clock()
    {
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        var legIdx = FindFirstLegElementIndex(bind);
        Assert.True(legIdx >= 0);
        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        Assert.True(
            Vector3.Distance(
                Corner(bind.Elements[legIdx].LocalToParent),
                Corner(walk.Elements[legIdx].LocalToParent)) > 0.05f);
    }

    [Fact]
    public void Cat_feline_setup_anim_evaluator_produces_walking_leg_pitches()
    {
        var state = PreviewRenderStateSynthesis.ForLivingWalk(1.25f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.feline.AdultCatModel",
            state,
            1.25f,
            out var pose,
            parityBuilderMethod: "Cat",
            isBaby: false));
        Assert.True(pose.Parts.TryGetValue("rightHindLeg", out var leg));
        Assert.True(MathF.Abs(leg.XRot) > 0.01f);
    }

    [Fact]
    public void Flat_quadruped_peer_position_strip_applies_to_feline_not_cow()
    {
        Assert.True(
            CleanRoomEntityModelRuntime.TryHasFlatQuadrupedPeerPositionAssignmentsForTests(
                "net.minecraft.client.model.animal.feline.AdultCatModel"));
        Assert.False(
            CleanRoomEntityModelRuntime.TryHasFlatQuadrupedPeerPositionAssignmentsForTests(
                "net.minecraft.client.model.animal.cow.CowModel"));
    }

    [Fact]
    public void Horse_setup_anim_hind_leg_pitch_varies_with_walk()
    {
        var rest = EquinePreviewState(0f, 0f);
        var walk = EquinePreviewState(1.25f, 0.3f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.equine.HorseModel",
            rest,
            0f,
            out var atRest));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.equine.HorseModel",
            walk,
            1.25f,
            out var animated));
        Assert.True(atRest.Parts.TryGetValue("leftHindLeg", out var restLeg));
        Assert.True(animated.Parts.TryGetValue("leftHindLeg", out var walkLeg));
        Assert.True(MathF.Abs(restLeg.XRot - walkLeg.XRot) > 1e-5f);
    }

    [Fact]
    public void Dolphin_setup_anim_body_pitch_varies_with_age_when_moving()
    {
        var rest = PreviewRenderStateSynthesis.ForLivingWalk(0f, 0.3f, 0.2f);
        var swim = PreviewRenderStateSynthesis.ForLivingWalk(2.5f, 0.3f, 0.2f);
        var restPose = new VanillaSetupAnimRuntime.PoseResult();
        var swimPose = new VanillaSetupAnimRuntime.PoseResult();
        GeometryIrEmitPolicy.ApplyDolphinSetupAnimPose(rest, restPose);
        GeometryIrEmitPolicy.ApplyDolphinSetupAnimPose(swim, swimPose);
        Assert.True(restPose.Parts.TryGetValue("body", out var restBody));
        Assert.True(swimPose.Parts.TryGetValue("body", out var swimBody));
        Assert.True(MathF.Abs(restBody.XRot - swimBody.XRot) > 1e-5f, "dolphin body pitch should vary with preview clock");
    }

    [Fact]
    public void Dolphin_setup_anim_swim_channels_require_is_moving_per_javap()
    {
        var state = new Dictionary<string, float>(PreviewRenderStateSynthesis.ForLivingWalk(2.5f, 0.3f, 0.2f), StringComparer.Ordinal)
        {
            ["isMoving"] = 0f,
        };
        var pose = new VanillaSetupAnimRuntime.PoseResult();
        GeometryIrEmitPolicy.ApplyDolphinSetupAnimPose(state, pose);
        Assert.True(pose.Parts.TryGetValue("body", out _));
        Assert.False(pose.Parts.ContainsKey("tail"));
        Assert.False(pose.Parts.ContainsKey("tailFin"));

        state["isMoving"] = 1f;
        GeometryIrEmitPolicy.ApplyDolphinSetupAnimPose(state, pose);
        Assert.True(pose.Parts.TryGetValue("tail", out var tail));
        Assert.True(pose.Parts.TryGetValue("tailFin", out var tailFin));
        Assert.True(MathF.Abs(tail.XRot) > 1e-5f);
        Assert.True(MathF.Abs(tailFin.XRot) > 1e-5f);
    }

    [Fact]
    public void Fox_setup_anim_evaluator_produces_time_varying_leg_pose()
    {
        var rest = PreviewRenderStateSynthesis.ForLivingWalk(0f, 0.3f, 0.2f);
        var walk = PreviewRenderStateSynthesis.ForLivingWalk(1.25f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.fox.FoxModel",
            rest,
            0f,
            out var atRest,
            parityBuilderMethod: "Fox",
            isBaby: false));
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.fox.FoxModel",
            walk,
            1.25f,
            out var animated,
            parityBuilderMethod: "Fox",
            isBaby: false));
        Assert.True(atRest.Parts.TryGetValue("rightHindLeg", out var restLeg));
        Assert.True(animated.Parts.TryGetValue("rightHindLeg", out var walkLeg));
        Assert.True(
            MathF.Abs(restLeg.XRot - walkLeg.XRot) > 1e-5f ||
            MathF.Abs(restLeg.ZRot - walkLeg.ZRot) > 1e-5f,
            "fox hind leg should vary with preview clock");
    }

    private static Dictionary<string, float> EquinePreviewState(float animationTimeSeconds, float idlePhase01) =>
        new(PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, 0.2f), StringComparer.Ordinal)
        {
            ["standAnimation"] = 0f,
            ["eatAnimation"] = 0f,
            ["feedingAnimation"] = 0f,
            ["ageScale"] = 1f,
            ["animateTail"] = 1f,
            ["isInWater"] = 0f
        };

    private static int FindFirstLegElementIndex(MergedJavaBlockModel model, float minHeight = 10f, float maxHeight = 14f)
    {
        for (var i = 0; i < model.Elements.Count; i++)
        {
            var e = model.Elements[i];
            var h = e.To[1] - e.From[1];
            if (h >= minHeight && h <= maxHeight)
            {
                return i;
            }
        }

        return -1;
    }
}
