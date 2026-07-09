using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png")]
    [InlineData("assets/minecraft/textures/entity/conduit/break_particle.png")]
    [InlineData("assets/minecraft/textures/entity/experience/experience_orb.png")]
    [InlineData("assets/minecraft/textures/entity/enderdragon/dragon_fireball.png")]
    public void HandLift_object_entity_paths_skip_living_entity_renderer_basis(string path)
    {
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.ConduitRenderer.createShellLayer",
            stemLower: "break_particle",
            normalizedAssetPath: path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bed/red.png", "red")]
    [InlineData("assets/minecraft/textures/entity/signs/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/banner/stripe_top.png", "stripe_top")]
    [InlineData("assets/minecraft/textures/entity/boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/oak.png", "oak")]
    [InlineData("assets/minecraft/textures/entity/chest_boat/bamboo.png", "bamboo")]
    [InlineData("assets/minecraft/textures/entity/minecart/minecart.png", "minecart")]
    public void ObjectEntity_paths_skip_living_entity_renderer_basis(string path, string stem)
    {
        var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: "net.minecraft.client.model.BedModel",
            stemLower: stem,
            normalizedAssetPath: path);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.Skip, basis);
        Assert.False(EntityGpuBoneFillPolicy.ShouldApplyStandardLivingPreviewBasis(path, stem), path);
    }
}
