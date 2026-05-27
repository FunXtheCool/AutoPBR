using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class ObfuscatedMeshFactoryDescriptorTests
{
    [Fact]
    public void IsMeshFactoryDescriptor_resolves_proguard_return_types_via_mappings()
    {
        var mapsPath = Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11",
            "client_mappings.txt");
        Assert.True(File.Exists(mapsPath), $"Missing test data: {mapsPath}");
        var maps = MojangMappingsParser.Load(mapsPath);
        Assert.True(maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.MeshDefinition", out var meshObf));

        var shortName = MojangMappingsParser.GetJavapClassArgForObfuscated(meshObf);
        var desc = $"(Lfoo;)L{shortName};";
        Assert.True(JvmClassFileParser.IsMeshFactoryDescriptor(desc, maps));
        Assert.False(JvmClassFileParser.IsMeshFactoryDescriptor(desc));
    }
}
