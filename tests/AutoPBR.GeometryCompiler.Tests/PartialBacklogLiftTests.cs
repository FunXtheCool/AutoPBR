using AutoPBR.Tests.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>T0 jar lifts for index partial backlog (26.1.2).</summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class PartialBacklogLiftTests
{
    private static readonly string[] PartialBacklogJvmNames =
    [
        "net.minecraft.client.model.animal.cow.WarmCowModel",
        "net.minecraft.client.model.animal.llama.LlamaSpitModel",
        "net.minecraft.client.model.animal.pig.ColdPigModel",
        "net.minecraft.client.model.effects.SpinAttackEffectModel",
        "net.minecraft.client.model.monster.endermite.EndermiteModel",
        "net.minecraft.client.model.monster.ghast.GhastModel",
        "net.minecraft.client.model.monster.piglin.AbstractPiglinModel",
        "net.minecraft.client.model.object.banner.BannerFlagModel",
        "net.minecraft.client.model.object.leash.LeashKnotModel",
        "net.minecraft.client.model.object.projectile.ArrowModel",
        "net.minecraft.client.model.object.projectile.ShulkerBulletModel",
        "net.minecraft.client.model.object.skull.PiglinHeadModel",
        "net.minecraft.client.model.object.skull.SkullModel",
    ];

    [Theory]
    [MemberData(nameof(PartialBacklogCases))]
    public void Partial_backlog_lifts_with_cuboids_from_jar(string officialJvmName)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        ClientJarIO.TryResolveJarEntry(jar, officialJvmName, null, out _, out var classBytes);
        var factoryMethod = MeshFactoryMethodResolver.Resolve(null, officialJvmName, "createBodyLayer", classBytes);
        var ok = GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null, officialJvmName, factoryMethod,
            preferAsm: true, out var attempt);
        Assert.True(ok,
            $"{officialJvmName}: notes={string.Join("; ", attempt.Notes)}");
        Assert.True(CountCuboids(attempt.Roots) > 0, officialJvmName);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.HumanoidModel")]
    [InlineData("net.minecraft.client.model.ambient.BatModel")]
    public void Reference_part_count_probe_when_bake_present(string officialJvmName)
    {
        var root = FindRepoRoot();
        var refPath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output",
            $"{officialJvmName}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{officialJvmName}.json");
        if (!File.Exists(refPath) || !File.Exists(irPath))
        {
            return;
        }

        using var reference = System.Text.Json.JsonDocument.Parse(File.ReadAllText(refPath));
        if (!string.Equals(reference.RootElement.GetProperty("extractionStatus").GetString(), "reference_java",
                StringComparison.Ordinal))
        {
            return;
        }

        using var ir = System.Text.Json.JsonDocument.Parse(File.ReadAllText(irPath));
        if (!string.Equals(ir.RootElement.GetProperty("extractionStatus").GetString(), "ok", StringComparison.Ordinal))
        {
            return;
        }

        var refParts = CountParts(reference.RootElement);
        var irParts = CountParts(ir.RootElement);
        Assert.True(irParts >= refParts - 1,
            $"{officialJvmName}: IR parts {irParts} should be within 1 of reference {refParts}");
    }

    public static IEnumerable<object[]> PartialBacklogCases() =>
        PartialBacklogJvmNames.Select(j => new object[] { j });

    private static string ResolveClientJar() =>
        GeometryIrTestTierSupport.TryClientJarPath(FindRepoRoot())
        ?? throw new InvalidOperationException("Minecraft client jar not found.");

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "AutoPBR.sln")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var r in roots)
        {
            if (r is JsonObject ro)
            {
                n += CountPart(ro);
            }
        }

        return n;
    }

    private static int CountPart(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray c ? c.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    n += CountPart(co);
                }
            }
        }

        return n;
    }

    private static int CountParts(System.Text.Json.JsonElement doc)
    {
        var n = 0;
        if (!doc.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var root in roots.EnumerateArray())
        {
            n += WalkPartCount(root);
        }

        return n;
    }

    private static int WalkPartCount(System.Text.Json.JsonElement part)
    {
        var n = 1;
        if (part.TryGetProperty("children", out var kids))
        {
            foreach (var ch in kids.EnumerateArray())
            {
                n += WalkPartCount(ch);
            }
        }

        return n;
    }
}
