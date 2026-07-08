using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class GhastPreviewAttachmentTests
{
    private readonly ITestOutputHelper _output;

    public GhastPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Ghast_family_runtime_keeps_javap_plus_y_cuboids_and_applies_standard_ler()
    {
        var monster = BuildGhastBindPose(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.MonsterJvm,
            64,
            32,
            out var monsterParts);
        var happy = BuildGhastBindPose(
            GhastPreviewTestLandmarks.HappyTexturePath,
            GhastPreviewTestLandmarks.HappyJvm,
            64,
            64,
            out var happyParts);

        Assert.True(monster.UsesLivingEntityRendererColumnYFlip);
        Assert.True(happy.UsesLivingEntityRendererColumnYFlip);
        AssertTentacleLocalY(monster, monsterParts, expectedHeight: 8f);
        AssertTentacleLocalY(happy, happyParts, expectedHeight: 5f);
        Assert.Equal(0.4f + 0.2f * MathF.Sin(2f), GeometryIrEmitPolicy.ComputeGhastAnimateTentaclesXRot(2, 0f), 3);
    }

    [Fact]
    public void Monster_ghast_runtime_mesh_is_not_cleanroom_hand_build_landmarks()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "GhastModel");
        GhastPreviewTestLandmarks.AssertNotCleanRoomBodyLocalCube(bind);
    }

    [Fact]
    public void Monster_ghast_runtime_mesh_body_and_tentacles_match_reference_java_landmarks()
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(GhastPreviewTestLandmarks.MonsterJvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "GhastModel");

        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            bind, partIds, GhastPreviewTestLandmarks.MonsterJvm, animationTimeSeconds: 0f);
        Assert.True(MeasurePreviewYSpan(bind) > 0.8f, "bind pose should span body plus pitched tentacles in preview Y");
    }

    [Fact]
    public void Happy_ghast_runtime_mesh_tentacles_hang_from_body_shell()
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(GhastPreviewTestLandmarks.HappyJvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            GhastPreviewTestLandmarks.HappyTexturePath,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "HappyGhastModel");

        var partIds = LoadPartIds(GhastPreviewTestLandmarks.HappyJvm, 64, 64);
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            bind, partIds, GhastPreviewTestLandmarks.HappyJvm, animationTimeSeconds: 0f);
    }

    [Fact]
    public void Ghast_shooting_variant_uses_monster_ghast_geometry_ir()
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(GhastPreviewTestLandmarks.MonsterJvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/ghast/ghast_shooting.png",
            GhastPreviewTestLandmarks.Profile26,
            0f,
            0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "GhastModel");
        GhastPreviewTestLandmarks.AssertNotCleanRoomBodyLocalCube(bind);
        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            bind, partIds, GhastPreviewTestLandmarks.MonsterJvm, animationTimeSeconds: 0f);
    }

    [Fact]
    public void Monster_ghast_runtime_gpu_mesh_tentacles_hang_below_body_shell()
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(GhastPreviewTestLandmarks.MonsterJvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.Profile26,
            0f,
            0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "GhastModel");

        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [GhastPreviewTestLandmarks.MonsterTexturePath] = 0
        };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [GhastPreviewTestLandmarks.MonsterTexturePath] = (64, 32)
        };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var (bodyMaxY, tentacleMinY, tentacleMaxY, hullGap) = MeasureBodyTentaclePreviewHullGap(gpuVerts, partIds);
        _output.WriteLine($"gpu bodyMaxY={bodyMaxY:F4} tentacleMinY={tentacleMinY:F4} tentacleMaxY={tentacleMaxY:F4} hullGap={hullGap:F4}");
        // GPU bind mesh uses the same root scale + LER basis as the CPU preview.
        Assert.True(hullGap < 0.15f, $"gpu tentacles should stay coupled to body shell (gap={hullGap:F3})");
        Assert.True(tentacleMaxY - tentacleMinY > 0.35f, "gpu tentacles should span visible length after bind-pose xRot");
        Assert.True(tentacleMaxY <= bodyMaxY + 0.25f, $"gpu tentacles should not pitch above body top (tentacleMaxY={tentacleMaxY:F3}, bodyMaxY={bodyMaxY:F3})");
        Assert.True(tentacleMinY < bodyMaxY, "gpu tentacles should extend below body top in preview Y");
    }

    [Fact]
    public void Monster_ghast_bind_pose_emits_exactly_ten_body_and_tentacle_cuboids()
    {
        var bind = BuildGhastBindPose(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.MonsterJvm,
            64,
            32,
            out var partIds);
        AssertGhastVisibleCuboidCounts(partIds, bind.Elements.Count);
    }

    [Fact]
    public void Happy_ghast_bind_pose_emits_exactly_ten_cuboids_without_equipment_parts()
    {
        var bind = BuildGhastBindPose(
            GhastPreviewTestLandmarks.HappyTexturePath,
            GhastPreviewTestLandmarks.HappyJvm,
            64,
            64,
            out var partIds);
        AssertGhastVisibleCuboidCounts(partIds, bind.Elements.Count);
        foreach (var id in partIds)
        {
            Assert.False(id.Contains("inner_body", StringComparison.OrdinalIgnoreCase), id);
            Assert.False(id.Contains("harness", StringComparison.OrdinalIgnoreCase), id);
            Assert.False(id.Contains("goggle", StringComparison.OrdinalIgnoreCase), id);
            Assert.False(id.Contains("rope", StringComparison.OrdinalIgnoreCase), id);
        }
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_body_and_tentacle_elements_emit_all_six_faces(
        string texturePath,
        string jvm,
        int atlasW,
        int atlasH)
    {
        var bind = BuildGhastBindPose(texturePath, jvm, atlasW, atlasH, out var partIds);
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            var id = partIds[i];
            if (!IsGhastBodyOrTentaclePart(id))
            {
                continue;
            }

            Assert.True(
                CountFaces(bind.Elements[i]) == 6,
                $"{texturePath} {id} should emit all six faces");
        }
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData("assets/minecraft/textures/entity/ghast/ghast_shooting.png", GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_baked_uvs_use_expected_body_unfold_not_degenerate_corners(
        string path,
        string jvm,
        int atlasW,
        int atlasH)
    {
        var bind = BuildGhastBindPose(path, jvm, atlasW, atlasH, out var partIds);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [path] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [path] = (atlasW, atlasH) };
        Assert.True(MinecraftModelBaker.TryBake(bind, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var bodyIdx = GhastPreviewTestLandmarks.FindBodyElementIndex(partIds);
        Assert.True(bodyIdx >= 0, "body part id missing");
        var body = bind.Elements[bodyIdx];
        var north = body.Faces["north"].Uv!;
        var javaNorth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.North, 0, 0, 16, 16, 16);
        Assert.Equal(javaNorth, north);

        AssertGhastBodyTentacleUvsWithinAtlas(verts!, bind, partIds, path);
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_tentacles_keep_direct_java_uv_slots(
        string path,
        string jvm,
        int atlasW,
        int atlasH)
    {
        var bind = BuildGhastBindPose(path, jvm, atlasW, atlasH, out var partIds);
        var tentacleIdx = partIds.FindIndex(id => string.Equals(id, "tentacle0", StringComparison.OrdinalIgnoreCase));
        Assert.True(tentacleIdx >= 0, "tentacle0 part id missing");

        var tentacle = bind.Elements[tentacleIdx];
        var height = Math.Max(1, (int)MathF.Round(MathF.Abs(tentacle.To[1] - tentacle.From[1])));
        var javaDown = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Down, 0, 0, 2, height, 2);
        var javaUp = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.Up, 0, 0, 2, height, 2);
        var javaNorth = EntityCuboidJavaUvConvention.GetUvRect(
            EntityCuboidJavaUvConvention.JavaDirection.North, 0, 0, 2, height, 2);

        Assert.Equal(javaUp, tentacle.Faces["up"].Uv);
        Assert.Equal(javaDown, tentacle.Faces["down"].Uv);
        Assert.Equal(javaNorth, tentacle.Faces["north"].Uv);
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_tentacle_local_y_extents_match_javap_add_box(
        string path,
        string jvm,
        int atlasW,
        int atlasH)
    {
        var bind = BuildGhastBindPose(path, jvm, atlasW, atlasH, out var partIds);
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            if (!partIds[i].StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var el = bind.Elements[i];
            var localMinY = MathF.Min(el.From[1], el.To[1]);
            var localMaxY = MathF.Max(el.From[1], el.To[1]);
            _output.WriteLine($"{path} {partIds[i]} localY=[{localMinY:F3},{localMaxY:F3}]");
            Assert.True(localMinY >= -1e-4f, $"{partIds[i]} should start at the javap attachment pivot (y=0)");
            Assert.True(localMaxY > 1e-4f, $"{partIds[i]} should preserve the javap +Y height");
        }
    }

    [Fact]
    public void Explore_cpu_rebake_matches_pack_converter_bake_for_ghast_bind_pose()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            GhastPreviewTestLandmarks.MonsterTexturePath,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 0f,
            out var merged,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, "GhastModel");
        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            merged, partIds, GhastPreviewTestLandmarks.MonsterJvm, animationTimeSeconds: 0f);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = (64, 32) };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var packVerts, out _, out _));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = GhastPreviewTestLandmarks.MonsterTexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = GhastPreviewTestLandmarks.Profile26.Name,
            NativeParsedVersion = GhastPreviewTestLandmarks.Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.3f,
            OrderedTextureZipPaths = [GhastPreviewTestLandmarks.MonsterTexturePath],
        };
        var materials = new[]
        {
            new PreviewTextureMaps
            {
                Width = 128,
                Height = 64,
                DiffuseRgba = new byte[128 * 64 * 4],
                NormalRgba = new byte[128 * 64 * 4],
                SpecularRgba = new byte[128 * 64 * 4],
                HeightRgba = new byte[128 * 64 * 4],
            }
        };
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            materials,
            animationTimeSeconds: 0f,
            out var rebakedVerts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(rebake.MeshProvenance!.Value, "GhastModel");

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        Assert.Equal(packVerts.Length, rebakedVerts!.Length);
        Assert.Equal(240, rebakedVerts.Length / stride);
        EntityPreviewPlacement.TryPopulateRebakeElementPartIds(
            rebake,
            new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2)),
            merged.Elements.Count);
        EntityPreviewPlacement.ApplyToPreviewVertices(packVerts, stride, rebake.ElementPartIds!);

        var maxErr = 0f;
        for (var i = 0; i + stride - 1 < packVerts.Length; i += stride)
        {
            var a = new Vector3(packVerts[i], packVerts[i + 1], packVerts[i + 2]);
            var b = new Vector3(rebakedVerts[i], rebakedVerts[i + 1], rebakedVerts[i + 2]);
            maxErr = MathF.Max(maxErr, Vector3.Distance(a, b));
        }

        _output.WriteLine($"ghast pack vs rebake max vertex err={maxErr:F5}");
        Assert.True(maxErr <= 0.02f, $"Explore CPU rebake diverged from pack-converter bake (maxErr={maxErr:F4})");
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, 128, 64, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, 128, 128, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_geometry_ir_bake_uses_shard_atlas_instead_of_padded_png_size(
        string texturePath,
        int physicalWidth,
        int physicalHeight,
        string officialJvm,
        int expectedWidth,
        int expectedHeight)
    {
        var profile = new MinecraftNativeProfile(
            "26.1.2",
            Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
            new Version(26, 1, 2));
        var provenance = new PreviewMeshProvenance(
            PreviewMeshDriverKind.RuntimeGeometryIrJson,
            officialJvm);
        var size = EntityGeometryIrTextureAtlas.ResolveForBake(
            texturePath,
            physicalWidth,
            physicalHeight,
            provenance,
            profile);

        Assert.Equal((expectedWidth, expectedHeight), size);
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_setup_anim_keeps_tentacles_attached_to_body(
        string path,
        string jvm,
        int atlasW,
        int atlasH)
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(jvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 2.5f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: true));

        var partIds = LoadPartIds(jvm, atlasW, atlasH);
        GhastPreviewTestLandmarks.AssertGhastRuntimeMatchesReferenceAffines(
            mesh, partIds, jvm, animationTimeSeconds: 2.5f);
    }

    private static MergedJavaBlockModel BuildGhastBindPose(
        string texturePath,
        string jvm,
        int atlasW,
        int atlasH,
        out List<string> partIds)
    {
        GhastPreviewTestLandmarks.RequireOkCommittedShardPath(jvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            GhastPreviewTestLandmarks.Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        GhastPreviewTestLandmarks.AssertRuntimeGeometryIrDriver(provenance, jvm.Split('.')[^1]);

        partIds = LoadPartIds(jvm, atlasW, atlasH);
        return bind;
    }

    private static List<string> LoadPartIds(string jvm, int atlasW, int atlasH)
    {
        var shardPath = GhastPreviewTestLandmarks.RequireOkCommittedShardPath(jvm);
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        return GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm });
    }

    private static void AssertGhastVisibleCuboidCounts(List<string> partIds, int elementCount)
    {
        var bodyCount = partIds.Count(id => string.Equals(id, "body", StringComparison.OrdinalIgnoreCase));
        var tentacleCount = partIds.Count(id => id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, bodyCount);
        Assert.Equal(9, tentacleCount);
        Assert.Equal(10, elementCount);
        Assert.Equal(10, partIds.Count);
    }

    private static bool IsGhastBodyOrTentaclePart(string id) =>
        string.Equals(id, "body", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase);

    private static void AssertTentacleLocalY(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        float expectedHeight)
    {
        var tentacleIdx = -1;
        for (var i = 0; i < partIds.Count; i++)
        {
            if (string.Equals(partIds[i], "tentacle0", StringComparison.OrdinalIgnoreCase))
            {
                tentacleIdx = i;
                break;
            }
        }

        Assert.True(tentacleIdx >= 0, "tentacle0 part id missing");
        var tentacle = mesh.Elements[tentacleIdx];
        Assert.Equal(0f, MathF.Min(tentacle.From[1], tentacle.To[1]), 3);
        Assert.Equal(expectedHeight, MathF.Max(tentacle.From[1], tentacle.To[1]), 3);
    }

    private static void AssertGhastBodyTentacleUvsWithinAtlas(
        float[] verts,
        MergedJavaBlockModel mesh,
        List<string> partIds,
        string label)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var vertexBase = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountFaces(mesh.Elements[e]) * 4;
            if (IsGhastBodyOrTentaclePart(partIds[e]))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vertexBase + v) * stride;
                    var u = verts[i + 6];
                    var vv = verts[i + 7];
                    Assert.True(u >= -1e-4f && u <= 1f + 1e-4f, $"{label} {partIds[e]} u={u}");
                    Assert.True(vv >= -1e-4f && vv <= 1f + 1e-4f, $"{label} {partIds[e]} v={vv}");
                }
            }

            vertexBase += vertCount;
        }
    }

    private static int CountFaces(ModelElement el)
    {
        var n = 0;
        foreach (var name in new[] { "north", "south", "west", "east", "up", "down" })
        {
            if (el.Faces.ContainsKey(name))
            {
                n++;
            }
        }

        return n;
    }

    private static (float BodyMaxY, float TentacleMinY, float TentacleMaxY, float HullGap) MeasureBodyTentaclePreviewHullGap(
        ReadOnlySpan<float> gpuVerts,
        List<string> partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var bodyMaxY = float.NegativeInfinity;
        var tentacleMinY = float.PositiveInfinity;
        var tentacleMaxY = float.NegativeInfinity;
        for (var i = 0; i + stride - 1 < gpuVerts.Length; i += stride)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(gpuVerts[i + 12]);
            if (bi < 0 || bi >= partIds.Count)
            {
                continue;
            }

            var preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(
                new Vector3(gpuVerts[i], gpuVerts[i + 1], gpuVerts[i + 2]));
            var id = partIds[bi];
            if (string.Equals(id, "body", StringComparison.OrdinalIgnoreCase))
            {
                bodyMaxY = MathF.Max(bodyMaxY, preview.Y);
            }
            else if (id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                tentacleMinY = MathF.Min(tentacleMinY, preview.Y);
                tentacleMaxY = MathF.Max(tentacleMaxY, preview.Y);
            }
        }

        return (bodyMaxY, tentacleMinY, tentacleMaxY, tentacleMinY - bodyMaxY);
    }

    private static float MeasurePreviewYSpan(MergedJavaBlockModel mesh)
    {
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var el in mesh.Elements)
        {
            ReadOnlySpan<(float x, float y, float z)> corners =
            [
                (el.From[0], el.From[1], el.From[2]), (el.To[0], el.From[1], el.From[2]),
                (el.From[0], el.To[1], el.From[2]), (el.To[0], el.To[1], el.From[2]),
                (el.From[0], el.From[1], el.To[2]), (el.To[0], el.From[1], el.To[2]),
                (el.From[0], el.To[1], el.To[2]), (el.To[0], el.To[1], el.To[2]),
            ];
            foreach (var (x, y, z) in corners)
            {
                var previewY = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(
                    Vector3.Transform(new Vector3(x, y, z), el.LocalToParent)).Y;
                minY = MathF.Min(minY, previewY);
                maxY = MathF.Max(maxY, previewY);
            }
        }

        return maxY - minY;
    }
}
