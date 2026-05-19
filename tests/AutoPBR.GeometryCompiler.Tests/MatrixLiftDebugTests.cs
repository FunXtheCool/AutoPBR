

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class MatrixLiftDebugTests
{
    [Fact]
    public void Silverfish_createBodyLayer_disassembly_contains_matrix_load_chain()
    {
        var jar = Path.Combine(FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.silverfish.SilverfishModel", null, out _, out var bytes));
        Assert.True(JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(bytes, "createBodyLayer", out var lines));

        var matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(bytes);
        Assert.True(matrices.ContainsKey("BODY_SIZES"));
        Assert.True(matrices.ContainsKey("BODY_TEXS"));

        var addBoxIdx = lines.FindIndex(l => l.Contains("addBox:(FFFFFF)", StringComparison.Ordinal));
        Assert.True(addBoxIdx >= 0);

        var window = lines.Skip(Math.Max(0, addBoxIdx - 25)).Take(25).ToList();
        Assert.Contains(window, l => l.Contains("i2f", StringComparison.Ordinal));
        Assert.Contains(window, l => l.Contains("aaload", StringComparison.Ordinal));
        Assert.Contains(window, l => l.Contains("iaload", StringComparison.Ordinal));
    }

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
