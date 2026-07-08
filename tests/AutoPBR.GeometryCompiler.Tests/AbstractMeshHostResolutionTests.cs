using AutoPBR.Tests.TestSupport;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class AbstractMeshHostResolutionTests
{
    [Theory]
    [InlineData("net.minecraft.client.model.animal.equine.AbstractEquineModel")]
    [InlineData("net.minecraft.client.model.monster.piglin.AbstractPiglinModel")]
    public void Bytecode_and_javap_mesh_resolution_succeed_for_abstract_hosts(string jvm)
    {
        var root = GeometryIrRepoPaths.FindRepoRoot();
        var jar = GeometryIrRepoPaths.ClientJar12111(root);
        if (jar is null)
        {
            return;
        }

        var mapsPath = Path.Combine(AppContext.BaseDirectory, "tools", "minecraft-parity", "1.21.11",
            "client_mappings.txt");
        if (!File.Exists(mapsPath))
        {
            mapsPath = GeometryIrRepoPaths.Mappings12111(root);
        }

        var maps = MojangMappingsParser.Load(mapsPath);
        Assert.True(maps.TryGetObfuscated(jvm, out var obf));
        Assert.True(ClientJarIO.TryResolveJarEntry(jar, jvm, obf, out _, out var classBytes));
        var staticMeshMethods = JvmClassFileParser.EnumerateMethods(classBytes)
            .Where(m => m.IsStatic && JvmClassFileParser.IsMeshFactoryDescriptor(m.Descriptor, maps))
            .Select(m => m.Name)
            .ToList();
        Assert.NotEmpty(staticMeshMethods);
        var concat = BytecodeMeshResolution.BuildMeshConcatDeep(jar, maps, jvm, classBytes, "createBodyLayer");
        Assert.False(string.IsNullOrWhiteSpace(concat), $"empty concat for {jvm}");
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals(concat), $"no mesh signals for {jvm}");
        Assert.True(
            concat.Contains("invokevirtual", StringComparison.Ordinal) &&
            JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(
                concat.Split('\n').First(l => l.Contains("invokevirtual", StringComparison.Ordinal) &&
                                              l.Contains("Ljava/lang/String;", StringComparison.Ordinal))),
            $"no binding line for {jvm}");

        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, maps, jvm, "createBodyLayer", out var bytecode),
            $"bytecode TryResolve failed for {jvm}; concatLen={concat.Length}");
        Assert.False(string.IsNullOrWhiteSpace(bytecode.MeshConcat));
        Assert.True(
            JavapMeshBytecodeProfiles.ContainsMeshSignals(bytecode.MeshConcat),
            $"no mesh signals in bytecode concat for {jvm}");

        var javap = GeometryJavapLocator.FindJavap();
        if (!string.IsNullOrEmpty(javap))
        {
            var disasm = GeometryLiftPipeline.TryResolveMeshDisassembly(javap, jar, maps, jvm);
            if (disasm is not null)
            {
                Assert.False(string.IsNullOrWhiteSpace(disasm.Value.MeshConcat));
            }
        }
    }
}
