using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class GhastPreviewAttachmentTests
{
    private readonly ITestOutputHelper _output;

    public GhastPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Ghast_family_tentacle_bind_pose_reorients_javap_plusY_box_to_model_space_hang_down()
    {
        var y0 = 0f;
        var y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            GhastPreviewTestLandmarks.MonsterJvm, "tentacle0", ref y0, ref y1));
        Assert.Equal(-8f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 5f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            GhastPreviewTestLandmarks.HappyJvm, "tentacle0", ref y0, ref y1));
        Assert.Equal(-5f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 5f;
        Assert.False(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            "net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel", "harness", ref y0, ref y1));
        Assert.Equal(0f, y0);
        Assert.Equal(5f, y1);

        y0 = 0f;
        y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            "net.minecraft.client.model.GhastModel", "tentacle0", ref y0, ref y1));
        Assert.Equal(-8f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            "net.minecraft.client.model.monster.Ghast.GhastModel", "tentacle0", ref y0, ref y1));
        Assert.Equal(-8f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            officialJvmName: null, "tentacle0", ref y0, ref y1, GhastPreviewTestLandmarks.MonsterTexturePath));
        Assert.Equal(-8f, y0);
        Assert.Equal(0f, y1);

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

        GhastPreviewTestLandmarks.AssertMonsterGhastReferenceWorldLandmarks(bind);
        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        GhastPreviewTestLandmarks.AssertGhastIrAssemblyLandmarks(bind, partIds, expectedBodyMaxY: -58.456f);
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
        GhastPreviewTestLandmarks.AssertGhastIrAssemblyLandmarks(bind, partIds, expectedBodyMaxY: -48.048f);
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
        GhastPreviewTestLandmarks.AssertGhastIrAssemblyLandmarks(bind, partIds, expectedBodyMaxY: -58.456f);
    }

    [Fact]
    public void Ghast_family_uv_footprint_uses_vanilla_texOffs_unfold_sizes()
    {
        var y0 = 0f;
        var y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            GhastPreviewTestLandmarks.MonsterJvm, "tentacle0", ref y0, ref y1));
        var uw = -1;
        var uh = -1;
        var ud = -1;
        Assert.True(GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint(
            GhastPreviewTestLandmarks.MonsterJvm, "body", y0, y1, ref uw, ref uh, ref ud));
        Assert.Equal(16, uw);
        Assert.Equal(16, uh);
        Assert.Equal(16, ud);

        uw = 8;
        uh = 8;
        ud = 8;
        Assert.True(GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint(
            GhastPreviewTestLandmarks.HappyJvm, "tentacle1", y0, y1, ref uw, ref uh, ref ud));
        Assert.Equal(2, uw);
        Assert.Equal(8, uh);
        Assert.Equal(2, ud);
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
        // GPU bind mesh uses preview-normalized coordinates (Skip LER); tentacles stay coupled under the body shell.
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
    public void Ghast_family_y_reoriented_tentacles_keep_java_uv_slots_on_reflected_planes(
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

        // The geometry is reflected from Java +Y to preview -Y, so the attachment plane is now "up"
        // while the free end is "down". The Java UV slot assignment must follow the original planes.
        Assert.Equal(javaDown, tentacle.Faces["up"].Uv);
        Assert.Equal(javaUp, tentacle.Faces["down"].Uv);
        Assert.Equal(new[] { javaNorth[0], javaNorth[3], javaNorth[2], javaNorth[1] }, tentacle.Faces["north"].Uv);
    }

    [Theory]
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64)]
    public void Ghast_family_tentacle_local_y_extents_hang_below_attachment_pivot(
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
            Assert.True(localMaxY <= 1e-4f, $"{partIds[i]} top should sit at attachment pivot (y=0)");
            Assert.True(localMinY < -1e-4f, $"{partIds[i]} should extend below attachment pivot");
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
        GhastPreviewTestLandmarks.AssertMonsterGhastReferenceWorldLandmarks(merged);
        var partIds = LoadPartIds(GhastPreviewTestLandmarks.MonsterJvm, 64, 32);
        GhastPreviewTestLandmarks.AssertGhastIrAssemblyLandmarks(merged, partIds, expectedBodyMaxY: -58.456f);

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
                Width = 64,
                Height = 32,
                DiffuseRgba = new byte[64 * 32 * 4],
                NormalRgba = new byte[64 * 32 * 4],
                SpecularRgba = new byte[64 * 32 * 4],
                HeightRgba = new byte[64 * 32 * 4],
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
    [InlineData(GhastPreviewTestLandmarks.MonsterTexturePath, GhastPreviewTestLandmarks.MonsterJvm, 64, 32, -58.456f)]
    [InlineData(GhastPreviewTestLandmarks.HappyTexturePath, GhastPreviewTestLandmarks.HappyJvm, 64, 64, -48.048f)]
    public void Ghast_family_setup_anim_keeps_tentacles_attached_to_body(
        string path,
        string jvm,
        int atlasW,
        int atlasH,
        float expectedBodyMaxY)
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
        GhastPreviewTestLandmarks.AssertGhastIrAssemblyLandmarks(
            mesh,
            partIds,
            expectedBodyMaxY,
            bodyMaxYTolerance: 0.35f,
            bindPose: false);
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

    private static void AssertGhastVisibleCuboidCounts(IReadOnlyList<string> partIds, int elementCount)
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

    private static void AssertGhastBodyTentacleUvsWithinAtlas(
        float[] verts,
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds,
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
        IReadOnlyList<string> partIds)
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
