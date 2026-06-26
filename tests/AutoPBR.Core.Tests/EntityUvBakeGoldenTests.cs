namespace AutoPBR.Core.Tests;

/// <summary>
/// Locks entity baked UV output while <see cref="PreviewUvBakePolicy"/> replaces global debug toggles.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class EntityUvBakeGoldenTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    public EntityUvBakeGoldenTests() => UvDebugSettings.ResetAllOverrides();

    [Theory]
    [InlineData("assets/minecraft/textures/entity/creeper/creeper.png", 64, 32, 14857558920194328997UL)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 64, 64, 1428835365211977893UL)]
    [InlineData("assets/minecraft/textures/entity/cat/cat_calico.png", 64, 32, 10285574214237588581UL)]
    [InlineData("assets/minecraft/textures/entity/dolphin/dolphin.png", 64, 64, 16782674017197654309UL)]
    [InlineData("assets/minecraft/textures/entity/ghast/ghast.png", 64, 32, 1082496271659415717UL)]
    public void Entity_baked_uv_fingerprint_is_stable(string texturePath, int atlasW, int atlasH, ulong goldenFingerprint)
    {
        var fp = BakeUvFingerprint(texturePath, atlasW, atlasH, useLogicalAtlasFromManifest: false);
        Assert.Equal(goldenFingerprint, fp);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png", 10557795273113060837UL)]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png", 294592320418992677UL)]
    [InlineData("assets/minecraft/textures/entity/camel/camel_baby.png", 17390841223401462245UL)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", 18632341434119397UL)]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png", 4052047643320367269UL)]
    [InlineData("assets/minecraft/textures/entity/cat/cat_calico.png", 10285574214237588581UL)]
    public void Baby_entity_baked_uv_fingerprint_is_stable_with_logical_atlas(string texturePath, ulong goldenFingerprint)
    {
        var fp = BakeUvFingerprint(texturePath, physicalW: 64, physicalH: 64, useLogicalAtlasFromManifest: true);
        Assert.Equal(goldenFingerprint, fp);
    }

    [Fact]
    public void Creeper_leg_visible_foot_undersides_keep_ler_transformed_up_face_uv()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/creeper/creeper.png", Profile26, 0f, 0f, out var merged, out _));
        Assert.True(merged.UsesLivingEntityRendererColumnYFlip);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = (64, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var footVerts = new List<(float u, float v, float y)>();
        for (var i = 0; i < verts.Length; i += stride)
        {
            if (verts[i + 4] > -0.9f)
            {
                continue;
            }

            footVerts.Add((verts[i + 6], verts[i + 7], verts[i + 1]));
        }

        Assert.True(footVerts.Count >= 4, "expected vertices on creeper foot undersides");
        var lowestY = footVerts.Min(t => t.y);
        var tips = footVerts.Where(t => t.y <= lowestY + 0.01f).ToList();
        Assert.True(tips.Count >= 4, "expected leg-tip underside verts");

        var meanU = tips.Average(t => t.u);
        var meanV = tips.Average(t => t.v);
        Assert.InRange(meanU, 0.145f, 0.17f);
        Assert.InRange(meanV, 0.54f, 0.59f);
    }

    [Fact]
    public void Entity_baseline_keeps_face_slots_direct_for_ler_models()
    {
        var baseline = PreviewUvBakePolicy.EntityCuboidBaseline;
        Assert.False(baseline.SwapFaceEastWest);
        Assert.False(baseline.SwapFaceUpDown);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/creeper/creeper.png", Profile26, 0f, 0f, out var creeper, out _));
        Assert.True(creeper.UsesLivingEntityRendererColumnYFlip);
        Assert.False(PreviewUvBakePolicy.Resolve(creeper).SwapFaceEastWest);
        Assert.False(PreviewUvBakePolicy.Resolve(creeper).SwapFaceUpDown);

        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/ghast/ghast.png", Profile26, 0f, 0f, out var ghast, out _));
        Assert.True(ghast.UsesLivingEntityRendererColumnYFlip);
        Assert.False(PreviewUvBakePolicy.Resolve(ghast).SwapFaceEastWest);
        Assert.False(PreviewUvBakePolicy.Resolve(ghast).SwapFaceUpDown);
    }

    [Fact]
    public void Entity_cuboid_baseline_matches_current_debug_defaults()
    {
        var baseline = PreviewUvBakePolicy.EntityCuboidBaseline;
        _ = baseline.WithDebugOverrides();
        Assert.False(baseline.FlipV);
        Assert.False(baseline.SwapFaceUpDown);
        Assert.False(baseline.SwapFaceEastWest);
        Assert.False(baseline.UseBottomLeftUvOrigin);
        Assert.True(baseline.MapJavaCuboidFaceCorners);
        Assert.True(baseline.PreserveDirectionalBounds);
        Assert.False(UvDebugSettings.HasActiveOverrides);
    }

    private static ulong BakeUvFingerprint(
        string texturePath,
        int physicalW,
        int physicalH,
        bool useLogicalAtlasFromManifest)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged, out var provenance));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = useLogicalAtlasFromManifest
                ? EntityGeometryIrTextureAtlas.ResolveForBake(ordered[i], physicalW, physicalH, provenance, Profile26)
                : (physicalW, physicalH);
        }

        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
        return ComputeUvFingerprint(verts, MinecraftModelBaker.FloatsPerVertex);
    }

    private static ulong ComputeUvFingerprint(ReadOnlySpan<float> verts, int stride)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            for (var i = 6; i < verts.Length; i += stride)
            {
                hash ^= BitConverter.SingleToUInt32Bits(verts[i]);
                hash *= 1099511628211UL;
                hash ^= BitConverter.SingleToUInt32Bits(verts[i + 1]);
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
