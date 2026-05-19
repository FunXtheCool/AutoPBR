

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GuardianFloatArrayExtractorTests
{
    [Fact]
    public void GuardianModel_clinit_exposes_spike_tables()
    {
        var jar = Path.Combine(FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.guardian.GuardianModel", null, out _, out var bytes));
        var arrays = JvmStaticFloatArrayExtractor.ExtractFromClass(bytes);
        Assert.True(arrays.ContainsKey("SPIKE_X"), string.Join(", ", arrays.Keys));
        Assert.Equal(12, arrays["SPIKE_X"].Length);
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

        throw new InvalidOperationException();
    }
}
