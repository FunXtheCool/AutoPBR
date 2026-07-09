using System.Text.Json;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BlockTextureParityJsonCatalogTests
{
    private static readonly Lazy<string[]> PngPaths = new(ReadInventoryPngPaths, isThreadSafe: true);

    public static IEnumerable<object[]> GetCataloguedPngPaths() => PngPaths.Value.Select(p => new object[] { p });

    private static string[] ReadInventoryPngPaths()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_block_textures.json");
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("files", out var files))
        {
            return [];
        }

        var list = new List<string>();
        foreach (var e in files.EnumerateArray())
        {
            if (!e.TryGetProperty("path", out var p))
            {
                continue;
            }

            var s = p.GetString();
            if (string.IsNullOrEmpty(s) || !s.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(s.Replace('\\', '/'));
        }

        return list.ToArray();
    }

    [Fact]
    public void Inventory_and_manifest_files_exist_with_matching_counts()
    {
        if (PngPaths.Value.Length == 0)
        {
            return;
        }

        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native",
            "minecraft_26.1.2_block_texture_model_manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var ruleCount = doc.RootElement.GetProperty("counts").GetProperty("rules").GetInt32();
        Assert.Equal(PngPaths.Value.Length, ruleCount);
    }

    [Theory]
    [MemberData(nameof(GetCataloguedPngPaths))]
    public void Catalogued_png_has_manifest_rule(string texturePath)
    {
        if (PngPaths.Value.Length == 0)
        {
            return;
        }

        var norm = texturePath.TrimStart('/');
        Assert.True(BlockTextureParityCatalog.IsCatalogued(norm));
        Assert.NotNull(BlockTextureParityCatalog.ResolveRule(norm));
    }
}
