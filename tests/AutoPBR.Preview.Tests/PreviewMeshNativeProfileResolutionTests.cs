
namespace AutoPBR.Preview.Tests;

public sealed class PreviewMeshNativeProfileResolutionTests
{
    [Fact]
    public void Unknown_profile_loads_cow_geometry_ir_shard_via_modern_label()
    {
        var unknown = new MinecraftNativeProfile("unknown", string.Empty, null);
        Assert.True(
            GeometryIrDocumentLoader.TryLoadLiftedOkForParity(
                unknown,
                "net.minecraft.client.model.animal.cow.CowModel",
                out _));
    }

    [Fact]
    public void Catalogued_cow_texture_uses_runtime_geometry_ir_with_unknown_profile()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        var unknown = new MinecraftNativeProfile("unknown", string.Empty, null);
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            unknown,
            idlePhase01: 0.2f,
            animationTimeSeconds: 1f,
            out _,
            out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
    }
}
