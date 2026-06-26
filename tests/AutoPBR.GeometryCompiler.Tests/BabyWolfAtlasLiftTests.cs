using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class BabyWolfAtlasLiftTests
{
    private const string Jvm = "net.minecraft.client.model.animal.wolf.BabyWolfModel";

    [Fact]
    public void Lifted_BabyWolfModel_documents_32x32_atlas_from_createBodyLayer()
    {
        var jar = Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(File.Exists(jar), $"Missing client.jar at {jar}");

        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(
                JavapLocator.FindJavap(),
                jar,
                null,
                Jvm,
                "createBodyLayer",
                preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));

        Assert.False(string.IsNullOrEmpty(attempt.MeshConcat));
        Assert.True(
            LayerDefinitionAtlasSizeProbe.TryReadPrimaryIsland(attempt.MeshConcat, out var w, out var h),
            "primary island LayerDefinition.create atlas not found");
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }
}
