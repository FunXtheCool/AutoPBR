using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Block-linked object entities (boat, chest boat, chest, minecart, banner, bell, bed, sign) use bytecode IR
/// or hand-lift with object-entity JVM routing and LER skip — not mob LivingEntityRenderer basis.
/// </summary>
public sealed partial class ObjectEntityBlockStateParityTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Theory]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/oak.png", "net.minecraft.client.model.object.boat.BoatModel")]
    [InlineData("Boat", "assets/minecraft/textures/entity/boat/bamboo.png", "net.minecraft.client.model.object.boat.RaftModel")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/oak.png", "net.minecraft.client.model.object.boat.BoatModel.createChestBoatModel")]
    [InlineData("ChestBoat", "assets/minecraft/textures/entity/chest_boat/bamboo.png", "net.minecraft.client.model.object.boat.RaftModel.createChestRaftModel")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal.png", "net.minecraft.client.model.object.chest.ChestModel")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal_left.png", "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyLeftLayer")]
    [InlineData("ChestEntity", "assets/minecraft/textures/entity/chest/normal_right.png", "net.minecraft.client.model.object.chest.ChestModel.createDoubleBodyRightLayer")]
    [InlineData("Minecart", "assets/minecraft/textures/entity/minecart/minecart.png", "net.minecraft.client.model.object.cart.MinecartModel")]
    [InlineData("Bell", "assets/minecraft/textures/entity/bell/bell_body.png", "net.minecraft.client.model.object.bell.BellModel")]
    [InlineData("BannerFlagStanding", "assets/minecraft/textures/entity/banner/stripe_top.png", "net.minecraft.client.model.object.banner.BannerFlagModel.standingPreviewComposite")]
    [InlineData("BannerFlagWall", "assets/minecraft/textures/entity/banner/banner_base.png", "net.minecraft.client.model.object.banner.BannerFlagModel.wallPreviewComposite")]
    [InlineData("Skull", "assets/minecraft/textures/entity/decorated_pot/skull_pottery_pattern.png", "net.minecraft.client.model.object.skull.SkullModel.previewComposite")]
    [InlineData("DecoratedPotEntity", "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png", "net.minecraft.client.model.DecoratedPotModel.previewComposite")]
    [InlineData("Bed", "assets/minecraft/textures/entity/bed/red.png", "net.minecraft.client.model.BedModel.previewComposite")]
    public void ObjectEntityJvmMap_resolves_expected_mesh_host(string builderMethod, string path, string expectedJvm)
    {
        var candidates = GeometryIrParityObjectEntityJvmMap.EnumerateCandidates(builderMethod, path).ToList();
        Assert.NotEmpty(candidates);
        Assert.Equal(expectedJvm, candidates[0]);
    }
}
