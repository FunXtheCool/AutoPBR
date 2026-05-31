namespace AutoPBR.Core.Tests;

/// <summary>
/// Locks LER <c>scale(-1,-1,1)</c> multiply order for flat <c>PartPose.offset</c> quadruped pilots
/// (<c>LocalToParent * S</c> vs default <c>S * LocalToParent</c>).
/// </summary>
public sealed partial class GeometryIrLerMirrorComposeClassificationTests
{
    private const string CowJvm = "net.minecraft.client.model.animal.cow.CowModel";
    private const string ColdCowJvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
    private const string WarmCowJvm = "net.minecraft.client.model.animal.cow.WarmCowModel";
    private const string PandaJvm = "net.minecraft.client.model.animal.panda.PandaModel";
    private const string PolarBearJvm = "net.minecraft.client.model.animal.polarbear.PolarBearModel";
    private const string BabyPandaJvm = "net.minecraft.client.model.animal.panda.BabyPandaModel";
    private const string BabyPolarBearJvm = "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel";
    private const string PigJvm = "net.minecraft.client.model.animal.pig.PigModel";
    private const string WolfJvm = "net.minecraft.client.model.animal.wolf.WolfModel";
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";
    private const string TurtleJvm = "net.minecraft.client.model.animal.turtle.TurtleModel";
    private const string QuadrupedJvm = "net.minecraft.client.model.QuadrupedModel";
    private const string HorseJvm = "net.minecraft.client.model.animal.equine.HorseModel";
    private const string AbstractFelineJvm = "net.minecraft.client.model.animal.feline.AbstractFelineModel";
    private const string AdultFelineJvm = "net.minecraft.client.model.animal.feline.AdultFelineModel";
    private const string AdultCatJvm = "net.minecraft.client.model.animal.feline.AdultCatModel";
    private const string BabyRabbitJvm = "net.minecraft.client.model.animal.rabbit.BabyRabbitModel";
    private const string RabbitJvm = "net.minecraft.client.model.animal.rabbit.RabbitModel";
}
