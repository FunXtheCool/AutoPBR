

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class SixFloatMatrixParseTests
{
    [Fact]
    public void Silverfish_folded_disassembly_parses_matrix_sixth_float()
    {
        var jar = Path.Combine(FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.silverfish.SilverfishModel", null, out _, out var bytes));
        Assert.True(JvmBytecodeDisassembler.TryDisassembleMethodToJavapLines(bytes, "createBodyLayer", out var raw));
        var lines = JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
            raw.Select(l => "     " + l.TrimStart()).ToList());
        var addBox = lines.FindIndex(l => l.Contains("addBox:(FFFFFF)", StringComparison.Ordinal));
        Assert.True(addBox > 0);

        var matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(bytes);
        var boxInts = new Dictionary<int, int> { [4] = 0 };
        Assert.True(JavapFloatGeometryMeshLift.DebugTryParseMatrixFloat(lines, addBox - 1, 0, boxInts, matrices,
            out var sz, out var trace), trace ?? "parse");
        Assert.Equal(matrices["BODY_SIZES"][0][2], sz);
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
