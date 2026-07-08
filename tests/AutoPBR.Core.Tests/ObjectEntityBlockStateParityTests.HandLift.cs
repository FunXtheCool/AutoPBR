using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/conduit/base.png", 1, 6f)]
    [InlineData("assets/minecraft/textures/entity/conduit/cage.png", 1, 8f)]
    [InlineData("assets/minecraft/textures/entity/conduit/break_particle.png", 1, 6f)]
    [InlineData("assets/minecraft/textures/entity/conduit/wind.png", 1, 16f)]
    public void Conduit_resolves_one_layer_cuboid_from_javap_hand_lift_shard(string path, int expectedElements, float expectedSpan)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(expectedElements, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var min, out var max);
        Assert.Equal(expectedSpan, max.X - min.X, 0.08f);
        Assert.Equal(expectedSpan, max.Y - min.Y, 0.08f);
        Assert.Equal(expectedSpan, max.Z - min.Z, 0.08f);
        Assert.Equal(8f, (min.X + max.X) * 0.5f, 0.08f);
        Assert.Equal(8f, (min.Y + max.Y) * 0.5f, 0.08f);
        Assert.Equal(8f, (min.Z + max.Z) * 0.5f, 0.08f);
    }

    [Fact]
    public void ExperienceOrb_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/experience/experience_orb.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void FishingHook_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/fishing/fishing_hook.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void DragonFireball_resolves_single_cuboid_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/enderdragon/dragon_fireball.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void BeaconBeam_resolves_two_beam_segments_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/beacon/beacon_beam.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
    }

    [Fact]
    public void GuardianBeam_resolves_single_beam_segment_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/guardian/guardian_beam.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }

    [Fact]
    public void EndPortalSurface_resolves_single_sheet_from_hand_lift_shard()
    {
        const string path = "assets/minecraft/textures/entity/end_portal/end_portal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Single(model.Elements);
    }
}
