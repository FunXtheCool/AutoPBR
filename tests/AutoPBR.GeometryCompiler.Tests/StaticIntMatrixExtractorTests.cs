using AutoPBR.Tests.TestSupport;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class StaticIntMatrixExtractorTests
{
    [Fact]
    public void SilverfishModel_clinit_exposes_body_tables()
    {
        if (ResolveClientJar() is not { } jar)
        {
            return;
        }

        var bytes = ReadClassBytes(jar, "net/minecraft/client/model/monster/silverfish/SilverfishModel");
        var matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(bytes);
        Assert.True(matrices.TryGetValue("BODY_SIZES", out var sizes));
        Assert.Equal(7, sizes.Length);
        Assert.Equal(3, sizes[0].Length);
        Assert.Equal(3, sizes[0][0]);
        Assert.True(matrices.TryGetValue("BODY_TEXS", out var texs));
        Assert.Equal(7, texs.Length);
    }

    [Fact]
    public void Obfuscated_silverfish_clinit_exposes_body_tables()
    {
        var root = FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client.jar");
        if (!File.Exists(jar))
        {
            return;
        }

        var mappings = MojangMappingsParser.Load(Path.Combine(root, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt"));
        Assert.True(mappings.TryGetObfuscated(
            "net.minecraft.client.model.monster.silverfish.SilverfishModel",
            out var obfuscated));
        Assert.True(ClientJarIO.TryResolveJarEntry(jar, "net.minecraft.client.model.monster.silverfish.SilverfishModel",
            obfuscated, out _, out var bytes));

        var matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(bytes);

        var sizes = Assert.Single(matrices.Values, v => v.Length == 7 && v[0].Length == 3 && v[0][0] == 3);
        Assert.Equal(3, sizes[0][0]);
    }

    private static byte[] ReadClassBytes(string jar, string officialJvmName)
    {
        Assert.True(ClientJarIO.TryResolveJarEntry(jar, officialJvmName, null, out _, out var bytes));
        return bytes;
    }

    private static string? ResolveClientJar() =>
        GeometryIrTestTierSupport.TryClientJarPath(FindRepoRoot());

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoPBR.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find repo root.");
    }
}
