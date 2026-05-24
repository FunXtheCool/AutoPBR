
namespace AutoPBR.Core.Tests;

/// <summary>Phase 9: newly lifted 1.21.11 geometry pilots (pig, creeper, humanoid, player).</summary>
public sealed class Phase9GeometryPilotOkTests
{
    private static readonly (string Jvm, string Factory)[] Pilots =
    [
        ("net.minecraft.client.model.animal.pig.PigModel", "createBodyLayer"),
        ("net.minecraft.client.model.monster.creeper.CreeperModel", "createBodyLayer"),
        ("net.minecraft.client.model.HumanoidModel", "createMesh"),
        ("net.minecraft.client.model.player.PlayerModel", "createMesh"),
    ];

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Committed_shard_is_ok_with_lifted_roots(string jvm, string factoryMethod)
    {
        var repo = FindRepoRoot();
        var path = Path.Combine(repo, "docs", "generated", "geometry", "1.21.11", $"{jvm}.json");
        Assert.True(File.Exists(path), path);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("ok", doc.RootElement.GetProperty("extractionStatus").GetString());
        Assert.Equal(factoryMethod, doc.RootElement.GetProperty("factoryMethod").GetString());
        Assert.True(doc.RootElement.GetProperty("roots").GetArrayLength() > 0);
    }

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void GeometryIrDocumentLoader_loads_shard_for_legacy_profile(string jvm, string _)
    {
        var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
        Assert.True(GeometryIrDocumentLoader.TryLoad(profile, jvm, out var root));
        Assert.Equal("ok", root.GetProperty("extractionStatus").GetString());
    }

    public static IEnumerable<object[]> PilotCases() =>
        Pilots.Select(p => new object[] { p.Jvm, p.Factory });

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
}
