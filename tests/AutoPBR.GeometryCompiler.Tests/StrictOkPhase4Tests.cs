using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class StrictOkPhase4Tests(ITestOutputHelper output)
{
    private static readonly string[] Phase4Models =
    [
        "net.minecraft.client.model.animal.squid.SquidModel",
        "net.minecraft.client.model.monster.silverfish.SilverfishModel",
        "net.minecraft.client.model.monster.endermite.EndermiteModel",
        "net.minecraft.client.model.monster.slime.MagmaCubeModel",
        "net.minecraft.client.model.monster.dragon.EnderDragonModel",
        "net.minecraft.client.model.effects.SpinAttackEffectModel"
    ];

    [Theory]
    [MemberData(nameof(Phase4ModelNames))]
    public void Phase4_models_pass_strict_shard_validation(string officialJvmName)
    {
        var jar = ResolveClientJar();
        if (jar is null)
        {
            return;
        }

        Assert.True(BytecodeGeometryMeshLift.TryLiftFromJar(jar, officialJvmName, "createBodyLayer", null,
            out var roots, out var notes, out _),
            string.Join("; ", notes));

        var shard = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["officialJvmName"] = officialJvmName,
            ["extractionStatus"] = "ok",
            ["roots"] = roots,
            ["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(roots)
        };

        var validation = GeometryIrStructuralValidator.ValidateShard(shard, officialJvmName,
            new GeometryIrStructuralValidator.Options(Strict: true));
        if (!validation.IsValid)
        {
            foreach (var issue in validation.Issues.Take(12))
            {
                output.WriteLine($"{issue.Code}: {issue.Message}");
            }
        }

        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    public static IEnumerable<object[]> Phase4ModelNames() =>
        Phase4Models.Select(m => new object[] { m });

    private static string? ResolveClientJar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoPBR.sln")))
            {
                return GeometryIrTestTierSupport.TryClientJarPath(dir.FullName);
            }

            dir = dir.Parent;
        }

        return null;
    }
}
