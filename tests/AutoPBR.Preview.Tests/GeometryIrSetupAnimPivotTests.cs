using System.Numerics;


namespace AutoPBR.Core.Tests;

public sealed class GeometryIrSetupAnimPivotTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Fact]
    public void Cat_geometry_ir_walk_leg_motion_hinges_at_part_origin()
    {
        const string path = "assets/minecraft/textures/entity/cat/cat_british_shorthair.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(bind.Elements.Count >= 10, $"expected full feline bind cuboid set, got {bind.Elements.Count}");
        Assert.True(walk.Elements.Count >= 10, $"expected full feline walk cuboid set, got {walk.Elements.Count}");
        var maxDelta = 0f;
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            maxDelta = MathF.Max(
                maxDelta,
                MathF.Abs(bind.Elements[i].LocalToParent.M41 - walk.Elements[i].LocalToParent.M41) +
                MathF.Abs(bind.Elements[i].LocalToParent.M42 - walk.Elements[i].LocalToParent.M42) +
                MathF.Abs(bind.Elements[i].LocalToParent.M43 - walk.Elements[i].LocalToParent.M43));
        }

        Assert.True(maxDelta > 0.05f, "expected visible feline setupAnim mesh change");
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cat/cat_all_black.png")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_red_baby.png")]
    public void Feline_geometry_ir_setup_anim_does_not_explode_bind_pose(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(bind.Elements.Count >= 10, $"expected full feline bind cuboid set, got {bind.Elements.Count}");
        Assert.False(TryHasCatastrophicRootStretch(bind, walk),
            "feline setupAnim should not scatter cuboids away from bind pose");
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png")]
    [InlineData("assets/minecraft/textures/entity/pig/pig_temperate.png")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf.png")]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png")]
    [InlineData("assets/minecraft/textures/entity/goat/goat.png")]
    [InlineData("assets/minecraft/textures/entity/creeper/creeper.png")]
    public void Flat_quadruped_family_setup_anim_overlay_does_not_explode_mesh(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.False(TryHasCatastrophicRootStretch(bind, walk),
            $"setupAnim overlay should not explode flat quadruped family mesh for {path}");
    }

    [Fact]
    public void Cow_geometry_ir_walk_leg_motion_hinges_at_part_origin_not_root()
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
        AssertLegWalkHingesNearPartOrigin(bind, walk, legIdx);
    }

    private static void AssertLegWalkHingesNearPartOrigin(
        MergedJavaBlockModel bind,
        MergedJavaBlockModel walk,
        int legIdx)
    {
        static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
        var bindCorner = Corner(bind.Elements[legIdx].LocalToParent);
        var walkCorner = Corner(walk.Elements[legIdx].LocalToParent);
        var bindDistFromRoot = bindCorner.Length();
        var walkDistFromRoot = walkCorner.Length();
        var delta = Vector3.Distance(bindCorner, walkCorner);

        Assert.True(delta > 0.05f, "expected visible leg swing");
        Assert.True(MathF.Abs(walkDistFromRoot - bindDistFromRoot) < bindDistFromRoot * 0.35f + 2f,
            $"leg corner should orbit near hinge, not swing wildly from root (bindDist={bindDistFromRoot:F2} walkDist={walkDistFromRoot:F2} delta={delta:F2})");
    }

    [Fact]
    public void Camel_geometry_ir_bind_pose_unchanged_when_setup_anim_overlay_disabled()
    {
        const string path = "assets/minecraft/textures/entity/camel/camel.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var atRest, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var atWalkPhase, out _));

        Assert.Equal(atRest.Elements.Count, atWalkPhase.Elements.Count);
        for (var i = 0; i < atRest.Elements.Count; i++)
        {
            Assert.Equal(atRest.Elements[i].LocalToParent, atWalkPhase.Elements[i].LocalToParent);
        }
    }

    [Fact]
    public void Camel_setup_anim_playback_resolves_walk_leg_rotation()
    {
        var state = PreviewRenderStateSynthesis.ForLivingWalk(1.25f, 0.3f, 0.2f);
        Assert.True(VanillaSetupAnimRuntime.TryEvaluate(
            "net.minecraft.client.model.animal.camel.CamelModel",
            state,
            1.25f,
            out var pose,
            parityBuilderMethod: "Camel",
            isBaby: false));
        Assert.True(pose.Parts.TryGetValue("right_front_leg", out var leg));
        Assert.True(MathF.Abs(leg.XRot) > 0.01f);
    }

    [Fact]
    public void Camel_geometry_ir_walk_leg_motion_does_not_stretch_from_root()
    {
        const string path = "assets/minecraft/textures/entity/camel/camel.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 0f,
            out var bind, out _));
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, idlePhase01: 0.3f, animationTimeSeconds: 1.25f,
            out var walk, out var provenance, applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        var maxDelta = 0f;
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            var a = bind.Elements[i].LocalToParent.M41;
            var b = walk.Elements[i].LocalToParent.M41;
            maxDelta = MathF.Max(maxDelta, MathF.Abs(a - b));
        }

        Assert.True(maxDelta > 0.05f, $"expected mesh change with setupAnim overlay (max corner delta={maxDelta:F3})");
        Assert.False(
            TryHasCatastrophicRootStretch(bind, walk),
            "walk animation should not explode cuboids away from bind pose");
    }

    private static bool TryHasCatastrophicRootStretch(MergedJavaBlockModel bind, MergedJavaBlockModel walk)
    {
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
            var bindCorner = Corner(bind.Elements[i].LocalToParent);
            var walkCorner = Corner(walk.Elements[i].LocalToParent);
            var bindDistFromRoot = bindCorner.Length();
            var walkDistFromRoot = walkCorner.Length();
            if (Vector3.Distance(bindCorner, walkCorner) <= 0.05f)
            {
                continue;
            }

            if (walkDistFromRoot > bindDistFromRoot * 2.5f + 4f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAssertAnyLegWalkHingesNearPartOrigin(
        MergedJavaBlockModel bind,
        MergedJavaBlockModel walk,
        float minHeight,
        float maxHeight)
    {
        var anyMotion = false;
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            var h = bind.Elements[i].To[1] - bind.Elements[i].From[1];
            if (h < minHeight || h > maxHeight)
            {
                continue;
            }

            static Vector3 Corner(Matrix4x4 m) => new(m.M41, m.M42, m.M43);
            var bindCorner = Corner(bind.Elements[i].LocalToParent);
            var walkCorner = Corner(walk.Elements[i].LocalToParent);
            var bindDistFromRoot = bindCorner.Length();
            var walkDistFromRoot = walkCorner.Length();
            var delta = Vector3.Distance(bindCorner, walkCorner);
            if (delta <= 0.05f)
            {
                continue;
            }

            anyMotion = true;
            if (MathF.Abs(walkDistFromRoot - bindDistFromRoot) >= bindDistFromRoot * 0.35f + 2f)
            {
                return false;
            }
        }

        return anyMotion;
    }

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
