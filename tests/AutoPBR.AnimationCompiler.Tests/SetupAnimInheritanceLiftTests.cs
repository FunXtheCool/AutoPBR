using AutoPBR.Tests.TestSupport;
using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class SetupAnimInheritanceLiftTests
{
    private static string? ClientJarPath =>
        GeometryIrTestTierSupport.TryClientJarPath(GeometryIrTestTierSupport.FindRepoRoot());

    [Fact]
    public void PigModel_without_setupAnim_inherits_QuadrupedModel()
    {
        if (ClientJarPath is not { } jar)
        {
            return;
        }

        var javap = JavapLocator.FindJavap();
        Assert.False(string.IsNullOrWhiteSpace(javap));
        const string pig = "net.minecraft.client.model.animal.pig.PigModel";
        Assert.True(JavapRunner.TryDisassemble(javap, jar, pig, out var disasm, out _));
        Assert.False(SetupAnimLift.TryLift(disasm, pig, out _, out _));
        Assert.True(
            SetupAnimInheritanceResolver.TryResolveSetupAnimHost(javap, jar, pig, disasm, out var host, out _, out _));
        Assert.Equal("net.minecraft.client.model.QuadrupedModel", host);
        Assert.True(SetupAnimLift.TryWriteInheritanceOnlyShard(pig, host, out var shard, out _));
        Assert.Equal(host, (string?)shard["inheritsSetupAnimFrom"]);
        Assert.Empty(shard["assignments"]!.AsArray());
    }

    [Fact]
    public void ZombieModel_without_setupAnim_inherits_HumanoidModel()
    {
        if (ClientJarPath is not { } jar)
        {
            return;
        }

        var javap = JavapLocator.FindJavap();
        Assert.False(string.IsNullOrWhiteSpace(javap));
        const string zombie = "net.minecraft.client.model.monster.zombie.ZombieModel";
        Assert.True(JavapRunner.TryDisassemble(javap, jar, zombie, out var disasm, out _));
        Assert.False(SetupAnimLift.TryLift(disasm, zombie, out _, out _));
        Assert.True(
            SetupAnimInheritanceResolver.TryResolveSetupAnimHost(
                javap, jar, zombie, disasm, out var host, out var hostDisasm, out _));
        Assert.Equal("net.minecraft.client.model.HumanoidModel", host);
        Assert.Contains("putfield", hostDisasm, StringComparison.Ordinal);
        Assert.True(SetupAnimLift.TryWriteInheritanceOnlyShard(zombie, host, out var shard, out _));
        Assert.Equal(host, (string?)shard["inheritsSetupAnimFrom"]);
    }
}
