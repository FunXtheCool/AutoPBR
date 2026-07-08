using AutoPBR.Tests.TestSupport;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class BabyWolfAtlasLiftTests
{
    private const string Jvm = "net.minecraft.client.model.animal.wolf.BabyWolfModel";

    [Fact]
    public void Lifted_BabyWolfModel_documents_32x32_atlas_from_createBodyLayer()
    {
        var jar = GeometryIrTestTierSupport.TryClientJarPath(Program.FindRepoRoot());
        if (jar is null)
        {
            return;
        }

        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(
                GeometryJavapLocator.FindJavap(),
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
