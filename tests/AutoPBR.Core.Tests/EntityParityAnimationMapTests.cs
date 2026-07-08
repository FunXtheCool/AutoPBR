using System.Text.Json;

using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EntityParityAnimationMapTests
{
    private static string NativeData(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", fileName);

    [Fact]
    public void Parity_animation_map_covers_all_batch_animation_classes()
    {
        var listPath = NativeData("minecraft_26.1.2_client_animation_definition_classes.txt");
        Assert.True(File.Exists(listPath), $"Missing test content: {listPath}");
        var expected = File.ReadAllLines(listPath)
            .Select(l => l.Trim().Replace('\\', '/'))
            .Where(l => l.Length > 0 && l.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[..^".class".Length].Replace('/', '.'))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var bindings = EntityParityAnimationMap.GetBindings();
        Assert.Equal(expected.Count, bindings.Count);
        var got = bindings.Select(b => b.AnimationOfficialJvmName).OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, got);
    }

    [Fact]
    public void Parity_animation_map_parity_builders_exist_in_entity_manifest()
    {
        var manifestPath = NativeData("minecraft_26.1.2_entity_texture_model_manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var builders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in doc.RootElement.GetProperty("rules").EnumerateArray())
        {
            builders.Add(e.GetProperty("builder_method").GetString()!);
        }

        foreach (var b in EntityParityAnimationMap.GetBindings())
        {
            foreach (var bm in b.ParityBuilderMethods)
            {
                Assert.Contains(bm, builders);
            }
        }
    }

    [Fact]
    public void Parity_animation_map_Armadillo_has_adult_and_baby_shards()
    {
        var adult = EntityParityAnimationMap.GetBindingsForParityBuilder("Armadillo")
            .Single(b => b.AnimationOfficialJvmName.Contains("ArmadilloAnimation", StringComparison.Ordinal) &&
                         !b.AnimationOfficialJvmName.Contains("Baby", StringComparison.Ordinal));
        Assert.True(adult.RestrictToBabyTextures == false);

        var baby = EntityParityAnimationMap.GetBindingsForParityBuilder("Armadillo")
            .Single(b => b.AnimationOfficialJvmName.Contains("BabyArmadillo", StringComparison.Ordinal));
        Assert.True(baby.RestrictToBabyTextures == true);
    }
}
