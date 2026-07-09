using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Baby wolf uses a dedicated 32×32 <c>BabyWolfModel</c> layer (javap <c>LayerDefinition.create(32,32)</c>),
/// not the adult 64×32 wolf atlas.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class BabyWolfUvDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Wolf_baby_rebake_uses_shard_32x32_atlas_not_manifest_placeholder()
    {
        const string texturePath = "assets/minecraft/textures/entity/wolf/wolf_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out _, out var provenance));

        var size = EntityGeometryIrTextureAtlas.ResolveForBake(texturePath, 64, 64, provenance, Profile26);
        Assert.Equal((32, 32), (size.Width, size.Height));
    }

    [Fact]
    public void Wolf_baby_rebaked_uv_fingerprint_matches_logical_32x32_atlas()
    {
        const string texturePath = "assets/minecraft/textures/entity/wolf/wolf_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var logical = EntityGeometryIrTextureAtlas.ResolveForBake(texturePath, 64, 64, provenance, Profile26);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [ordered[0]] = logical,
        };
        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var baked, out _, out _));

        var fp = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMeshUvFingerprint(
            baked, MinecraftModelBaker.FloatsPerVertex);
        Assert.Equal(4052047643320367269UL, fp);
    }
}
