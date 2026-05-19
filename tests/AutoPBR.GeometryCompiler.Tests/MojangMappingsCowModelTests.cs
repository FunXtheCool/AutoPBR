using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class MojangMappingsCowModelTests
{
    [Fact]
    public void Client_mappings_1_21_11_resolve_animal_cow_CowModel_to_obfuscated_type()
    {
        var mapsPath = Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");
        Assert.True(File.Exists(mapsPath), $"Missing test data: {mapsPath}");
        var parser = MojangMappingsParser.Load(mapsPath);
        const string official = "net.minecraft.client.model.animal.cow.CowModel";
        Assert.True(parser.TryGetObfuscated(official, out var obf), "Expected CowModel mapping line.");
        Assert.Equal("net.minecraft.client.model.animal.cow.hak", obf, StringComparer.Ordinal);
        Assert.Equal("hak", MojangMappingsParser.GetJavapClassArgForObfuscated(obf), StringComparer.Ordinal);
    }
}
