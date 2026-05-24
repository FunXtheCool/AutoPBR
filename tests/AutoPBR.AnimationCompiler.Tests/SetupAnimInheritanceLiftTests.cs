
using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class SetupAnimInheritanceLiftTests
{
    private static string ClientJarPath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tools", "minecraft-parity", "26.1.2", "client.jar"));

    [Fact]
    public void PigModel_without_setupAnim_inherits_QuadrupedModel()
    {
        Assert.True(File.Exists(ClientJarPath), $"missing {ClientJarPath}");
        var javap = JavapLocator.FindJavap();
        Assert.False(string.IsNullOrWhiteSpace(javap));
        const string pig = "net.minecraft.client.model.animal.pig.PigModel";
        Assert.True(JavapRunner.TryDisassemble(javap, ClientJarPath, pig, out var disasm, out _));
        Assert.False(SetupAnimLift.TryLift(disasm, pig, out _, out _));
        Assert.True(
            SetupAnimInheritanceResolver.TryResolveSetupAnimHost(javap, ClientJarPath, pig, disasm, out var host, out _, out _));
        Assert.Equal("net.minecraft.client.model.QuadrupedModel", host);
        Assert.True(SetupAnimLift.TryWriteInheritanceOnlyShard(pig, host, out var shard, out _));
        Assert.Equal(host, (string?)shard["inheritsSetupAnimFrom"]);
        Assert.Empty(shard["assignments"]!.AsArray());
    }

    [Fact]
    public void ZombieModel_without_setupAnim_inherits_HumanoidModel()
    {
        Assert.True(File.Exists(ClientJarPath), $"missing {ClientJarPath}");
        var javap = JavapLocator.FindJavap();
        Assert.False(string.IsNullOrWhiteSpace(javap));
        const string zombie = "net.minecraft.client.model.monster.zombie.ZombieModel";
        Assert.True(JavapRunner.TryDisassemble(javap, ClientJarPath, zombie, out var disasm, out _));
        Assert.False(SetupAnimLift.TryLift(disasm, zombie, out _, out _));
        Assert.True(
            SetupAnimInheritanceResolver.TryResolveSetupAnimHost(
                javap, ClientJarPath, zombie, disasm, out var host, out var hostDisasm, out _));
        Assert.Equal("net.minecraft.client.model.HumanoidModel", host);
        Assert.Contains("putfield", hostDisasm, StringComparison.Ordinal);
        Assert.True(SetupAnimLift.TryWriteInheritanceOnlyShard(zombie, host, out var shard, out _));
        Assert.Equal(host, (string?)shard["inheritsSetupAnimFrom"]);
    }
}
