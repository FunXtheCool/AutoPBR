using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class MultilayerPilotLiftTests
{
    private static readonly string[] MultilayerJvmNames =
    [
        "net.minecraft.client.model.effects.SpinAttackEffectModel",
        "net.minecraft.client.model.monster.witch.WitchModel",
        "net.minecraft.client.model.monster.strider.AdultStriderModel",
        "net.minecraft.client.model.monster.strider.StriderModel",
        "net.minecraft.client.model.monster.nautilus.ZombieNautilusCoralModel",
        "net.minecraft.client.model.player.PlayerCapeModel",
    ];

    [Theory]
    [MemberData(nameof(MultilayerCases))]
    public void Multilayer_pilot_lifts_from_jar(string officialJvmName)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        var factory = officialJvmName.Contains("SpinAttack", StringComparison.Ordinal)
            ? "createLayer"
            : officialJvmName.Contains("PlayerCape", StringComparison.Ordinal)
                ? "createCapeLayer"
                : "createBodyLayer";
        Assert.True(GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null, officialJvmName, factory,
                preferAsm: true, out var attempt),
            string.Join("; ", attempt.Notes));
        Assert.True(CountCuboids(attempt.Roots) > 0, officialJvmName);
        if (officialJvmName.Contains("PlayerCape", StringComparison.Ordinal))
        {
            Assert.Contains("clearRecursively", attempt.MeshConcat, StringComparison.Ordinal);
            var head = FindPartById(attempt.Roots, "head");
            Assert.NotNull(head);
            Assert.Empty(head!["cuboids"]!.AsArray());
            var cape = FindPartById(attempt.Roots, "cape");
            Assert.NotNull(cape);
            Assert.NotEmpty(cape!["cuboids"]!.AsArray());
        }
    }

    public static IEnumerable<object[]> MultilayerCases() =>
        MultilayerJvmNames.Select(j => new object[] { j });

    private static string ResolveClientJar()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(File.Exists(path), $"Missing client.jar at {path}");
        return path;
    }

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

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindPartById(ro, id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPartById(JsonObject part, string id, out JsonObject? found)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co && TryFindPartById(co, id, out found))
                {
                    return true;
                }
            }
        }

        found = null;
        return false;
    }
}
