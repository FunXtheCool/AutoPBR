using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EquineGpuSkinningPrepTests
{
    [Fact]
    public void Horse_white_bind_pose_and_animated_merged_models_have_same_element_count()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/horse/horse_white.png";
        var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
        const float idle = 0.28f;

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, animationTimeSeconds: 0f, out var bind));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, animationTimeSeconds: 3.33f, out var lift));

        Assert.Equal(bind.Elements.Count, lift.Elements.Count);
    }

    [Fact]
    public void Horse_white_ordered_texture_paths_identical_at_bind_and_animated_clock()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/horse/horse_white.png";
        var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
        const float idle = 0.28f;
        const string ns = "minecraft";

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 0f, out var bind));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 4.2f, out var lift));

        var ob = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(bind, ns);
        var ol = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(lift, ns);
        Assert.Equal(ob.Count, ol.Count);
        for (var i = 0; i < ob.Count; i++)
        {
            Assert.Equal(ob[i], ol[i], ignoreCase: true);
        }
    }

    [Fact]
    public void TryFillEmulatedEntityBoneMatrices_horse_neck_matrices_differ_across_animation_clock()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/horse/horse_white.png";
        var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
        const float idle = 0.28f;

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 0.1f, out var a));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 2.7f, out var b));

        // Head_parts is element index 1 in AbstractEquine-style rigs (body is 0).
        Assert.True(a.Elements.Count > 1 && b.Elements.Count > 1);
        var ma = a.Elements[1].LocalToParent;
        var mb = b.Elements[1].LocalToParent;
        var diff = Math.Abs(ma.M43 - mb.M43) + Math.Abs(ma.M42 - mb.M42) + Math.Abs(ma.M41 - mb.M41);
        Assert.True(diff > 1e-4f, "expected neck/head_parts pose to vary with preview animation clock");
    }
}
