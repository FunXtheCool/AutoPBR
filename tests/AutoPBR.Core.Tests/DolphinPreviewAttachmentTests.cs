using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class DolphinPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string Jvm = "net.minecraft.client.model.animal.dolphin.DolphinModel";
    private const string TexturePath = "assets/minecraft/textures/entity/dolphin/dolphin.png";

    private readonly ITestOutputHelper _output;

    public DolphinPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Explore_cpu_rebake_matches_pack_converter_bake_for_bind_pose()
    {
        const string texturePath = "assets/minecraft/textures/entity/dolphin/dolphin.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 0f,
            out var merged,
            applyGeometryIrSetupAnimMotion: false));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var packVerts, out _, out _));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = texturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.3f,
            OrderedTextureZipPaths = [texturePath],
        };
        var materials = new[]
        {
            new PreviewTextureMaps
            {
                Width = 64,
                Height = 64,
                DiffuseRgba = new byte[64 * 64 * 4],
                NormalRgba = new byte[64 * 64 * 4],
                SpecularRgba = new byte[64 * 64 * 4],
                HeightRgba = new byte[64 * 64 * 4],
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

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        Assert.Equal(packVerts.Length, rebakedVerts!.Length);
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

        _output.WriteLine($"pack vs rebake max vertex err={maxErr:F5}");
        Assert.True(maxErr <= 0.02f, $"Explore CPU rebake diverged from pack-converter bake (maxErr={maxErr:F4})");

        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            merged, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) &&
            string.Equals(status, "ok", StringComparison.Ordinal))
        {
            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
            var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
                repaired,
                GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });
            var (_, _, hullGapX) = MeasureBodyFinHullGap(gpuVerts, partIds);
            var (_, _, _, _, _, hullGapY) = MeasureBodyFinPreviewExtents(gpuVerts, partIds);
            _output.WriteLine($"bind mesh hullGapX={hullGapX:F4} hullGapY={hullGapY:F4}");
            Assert.True(hullGapY < 0.35f, $"dolphin fins detached vertically in bind mesh (gap={hullGapY:F3})");
        }
    }

    [Fact]
    public void Runtime_mesh_fin_preview_affine_matches_hand_builder()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm, UseColumnTranslationTimesRotationPartPose = true });

        var hand = CleanRoomEntityModelRuntime.BuildDolphinHandMeshForTests(TexturePath, Profile26);
        var handPartOrder = new[] { "body", "back_fin", "left_fin", "right_fin", "tail", "tail_fin", "head", "nose" };
        var handIdxByPart = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < handPartOrder.Length && i < hand.Elements.Count; i++)
        {
            handIdxByPart[handPartOrder[i]] = i;
        }

        foreach (var finId in new[] { "left_fin", "right_fin", "back_fin" })
        {
            Assert.True(handIdxByPart.TryGetValue(finId, out var handIdx), finId);
            var handWorld = hand.Elements[handIdx].LocalToParent;
            Matrix4x4? meshWorld = null;
            for (var i = 0; i < bind.Elements.Count; i++)
            {
                if (string.Equals(partIds[i], finId, StringComparison.Ordinal))
                {
                    meshWorld = bind.Elements[i].LocalToParent;
                    break;
                }
            }

            Assert.NotNull(meshWorld);
            foreach (var probe in new[] { Vector3.Zero, new Vector3(0f, -3f, 0.5f), Vector3.UnitX })
            {
                var handPreview = Vector3.Transform(probe, handWorld);
                var meshPreview = Vector3.Transform(probe, meshWorld.Value);
                _output.WriteLine($"{finId} probe={probe}: hand={handPreview} mesh={meshPreview}");
                Assert.True(Vector3.Distance(handPreview, meshPreview) <= 0.05f,
                    $"{finId} probe={probe}: hand={handPreview} mesh={meshPreview}");
            }
        }

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var (bodyMaxX, finMinX, hullGap) = MeasureBodyFinHullGap(gpuVerts, partIds);
        var (_, _, _, _, _, hullGapY) = MeasureBodyFinPreviewExtents(gpuVerts, partIds);
        _output.WriteLine($"bodyMaxX={bodyMaxX:F4} finMinX={finMinX:F4} hullGapX={hullGap:F4} hullGapY={hullGapY:F4}");
        Assert.True(hullGap < 0.15f, $"fin hull gap from body too large ({hullGap:F3})");
        Assert.True(hullGapY < 0.35f, $"fin vertical gap from body too large ({hullGapY:F3})");
    }

    [Fact]
    public void Runtime_mesh_fin_stays_attached_to_body_when_setup_anim_pitches_body()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        const float anim = 2.5f;
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath,
            Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: anim,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mesh, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, GeometryIrMeshEmitOptions.ForParity(64, 64));
        var (bodyMaxX, finMinX, hullGap) = MeasureBodyFinHullGap(gpuVerts, partIds);
        _output.WriteLine($"anim bodyMaxX={bodyMaxX:F4} finMinX={finMinX:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.35f, $"fin detached from body under setupAnim (gap={hullGap:F3})");
    }

    [Fact]
    public void Hand_built_fin_corners_match_ir_mesh_in_preview_space()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var ir = CleanRoomEntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            TexturePath, Jvm, 64, 64, repaired, out var err);
        Assert.NotNull(ir);
        Assert.Null(err);

        var hand = CleanRoomEntityModelRuntime.BuildDolphinHandMeshForTests(TexturePath, Profile26);
        var ler = CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale;
        var irPartIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired, GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm, UseColumnTranslationTimesRotationPartPose = true });

        // Hand BuildDolphin emit order: body, back_fin, left_fin, right_fin, tail, tail_fin, head, nose.
        var handPartOrder = new[] { "body", "back_fin", "left_fin", "right_fin", "tail", "tail_fin", "head", "nose" };
        var handIdxByPart = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < handPartOrder.Length && i < hand.Elements.Count; i++)
        {
            handIdxByPart[handPartOrder[i]] = i;
        }

        foreach (var finId in new[] { "left_fin", "right_fin", "back_fin", "tail", "tail_fin" })
        {
            var irIdx = irPartIds.FindIndex(id => string.Equals(id, finId, StringComparison.Ordinal));
            Assert.True(irIdx >= 0, finId);
            Assert.True(handIdxByPart.TryGetValue(finId, out var handIdx), finId);
            var irWorld = ir!.Elements[irIdx].LocalToParent;
            var handEl = hand.Elements[handIdx];
            foreach (var probe in new[] { Vector3.Zero, new Vector3(0f, -3f, 0.5f), Vector3.UnitX })
            {
                var irPreview = Vector3.Transform(Vector3.Transform(probe, irWorld), ler);
                var handPreview = Vector3.Transform(probe, handEl.LocalToParent);
                _output.WriteLine($"{finId} probe={probe}: hand={handPreview} ir={irPreview}");
                Assert.True(Vector3.Distance(handPreview, irPreview) <= 0.08f,
                    $"{finId} probe={probe}: hand={handPreview} ir={irPreview}");
            }
        }
    }

    [Fact]
    public void Runtime_mesh_part_origins_match_hand_builder_in_preview_space()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath, Profile26, 0f, 0f, out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        var hand = CleanRoomEntityModelRuntime.BuildDolphinHandMeshForTests(TexturePath, Profile26);
        var handPartOrder = new[] { "body", "back_fin", "left_fin", "right_fin", "tail", "tail_fin", "head", "nose" };
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm, UseColumnTranslationTimesRotationPartPose = true });

        for (var i = 0; i < handPartOrder.Length; i++)
        {
            var partId = handPartOrder[i];
            var irIdx = partIds.FindIndex(id => string.Equals(id, partId, StringComparison.Ordinal));
            if (irIdx < 0)
            {
                continue;
            }

            var handOrigin = Vector3.Transform(Vector3.Zero, hand.Elements[i].LocalToParent);
            var irOrigin = Vector3.Transform(Vector3.Zero, mesh!.Elements[irIdx].LocalToParent);
            _output.WriteLine($"{partId}: hand={handOrigin} ir={irOrigin}");
            Assert.True(Vector3.Distance(handOrigin, irOrigin) <= 0.08f, $"{partId}: hand={handOrigin} ir={irOrigin}");
        }
    }

    [Fact]
    public void Dolphin_jvm_enables_column_translation_times_rotation_pose_compose()
    {
        Assert.True(GeometryIrMeshEmitOptions.UsesColumnTranslationTimesRotationPartPoseJvm(
            "net.minecraft.client.model.animal.dolphin.DolphinModel"));
        Assert.True(GeometryIrMeshEmitOptions.UsesColumnTranslationTimesRotationPartPoseJvm(
            "net.minecraft.client.model.animal.dolphin.BabyDolphinModel"));
        Assert.False(GeometryIrMeshEmitOptions.UsesColumnTranslationTimesRotationPartPoseJvm(
            "net.minecraft.client.model.animal.rabbit.AdultRabbitModel"));
    }

    [Fact]
    public void Dolphin_jvm_auto_column_compose_attaches_fins_without_explicit_flag()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            TexturePath,
            Profile26,
            Jvm,
            64,
            64,
            out var err,
            geometryRootOverride: repaired);
        Assert.Null(err);
        Assert.NotNull(mesh);

        var hand = CleanRoomEntityModelRuntime.BuildDolphinHandMeshForTests(TexturePath, Profile26);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });
        foreach (var finId in new[] { "left_fin", "right_fin", "back_fin" })
        {
            var handIdx = finId switch { "back_fin" => 1, "left_fin" => 2, "right_fin" => 3, _ => -1 };
            Assert.True(handIdx >= 0, finId);
            var handOrigin = Vector3.Transform(Vector3.Zero, hand.Elements[handIdx].LocalToParent);
            Matrix4x4? meshWorld = null;
            for (var i = 0; i < mesh!.Elements.Count; i++)
            {
                if (string.Equals(partIds[i], finId, StringComparison.Ordinal))
                {
                    meshWorld = mesh.Elements[i].LocalToParent;
                    break;
                }
            }

            Assert.NotNull(meshWorld);
            var meshOrigin = Vector3.Transform(Vector3.Zero, meshWorld.Value);
            var gap = Vector3.Distance(handOrigin, meshOrigin);
            _output.WriteLine($"{finId}: hand={handOrigin} mesh={meshOrigin} gap={gap:F3}");
            Assert.True(gap <= 0.08f, $"{finId} JVM auto column compose should match hand builder (gap={gap:F3})");
        }
    }

    [Fact]
    public void Runtime_mesh_with_setup_anim_preserves_fin_bone_palette_at_bind_clock()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        const float idle = 0.3f;
        const float anim = 1.25f;
        var scratch = new List<Matrix4x4>();
        Assert.True(runtime.TryFillBoneMatricesFast(TexturePath, Profile26, idle, anim, scratch, out var boneCount, null));
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, idle, anim, out var merged, out var provenance,
            applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Equal(merged.Elements.Count, boneCount);
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(AllClose(scratch[i], merged.Elements[i].LocalToParent), $"bone {i} mismatch");
        }
    }

    private static bool AllClose(in Matrix4x4 a, in Matrix4x4 b, float eps = 2e-4f) =>
        Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps && Math.Abs(a.M43 - b.M43) <= eps;

    private static Dictionary<string, Matrix4x4> BuildReferencePartWorldMatrices(JsonElement referenceRoot)
    {
        var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        if (!referenceRoot.TryGetProperty("roots", out var roots))
        {
            return map;
        }

        foreach (var root in roots.EnumerateArray())
        {
            VisitReferencePart(root, Matrix4x4.Identity, map);
        }

        return map;

        static void VisitReferencePart(JsonElement part, Matrix4x4 parentWorld, Dictionary<string, Matrix4x4> sink)
        {
            var world = parentWorld;
            if (part.TryGetProperty("pose", out var poseEl) &&
                CleanRoomEntityModelRuntime.TryComposePartPosePublic(poseEl, parentWorld, out var worldTexel))
            {
                world = worldTexel;
            }

            if (part.TryGetProperty("id", out var idEl))
            {
                var id = idEl.GetString() ?? "";
                if (id.Length > 0)
                {
                    sink[id] = world;
                }
            }

            if (part.TryGetProperty("children", out var children))
            {
                foreach (var ch in children.EnumerateArray())
                {
                    VisitReferencePart(ch, world, sink);
                }
            }
        }
    }

    private static (float BodyMaxX, float FinMinX, float HullGap) MeasureBodyFinHullGap(
        ReadOnlySpan<float> gpuVerts,
        IReadOnlyList<string> partIds)
    {
        var extents = MeasureBodyFinPreviewExtents(gpuVerts, partIds);
        return (extents.BodyMaxX, extents.FinMinX, extents.HullGapX);
    }

    private static (float BodyMaxX, float FinMinX, float BodyMaxY, float FinMinY, float HullGapX, float HullGapY)
        MeasureBodyFinPreviewExtents(
            ReadOnlySpan<float> gpuVerts,
            IReadOnlyList<string> partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var bodyMaxX = float.NegativeInfinity;
        var finMinX = float.PositiveInfinity;
        var bodyMaxY = float.NegativeInfinity;
        var finMinY = float.PositiveInfinity;
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
            if (id.Contains("body", StringComparison.Ordinal) && !id.Contains("inner", StringComparison.Ordinal))
            {
                bodyMaxX = MathF.Max(bodyMaxX, preview.X);
                bodyMaxY = MathF.Max(bodyMaxY, preview.Y);
            }
            else if (id.Contains("fin", StringComparison.Ordinal))
            {
                finMinX = MathF.Min(finMinX, preview.X);
                finMinY = MathF.Min(finMinY, preview.Y);
            }
        }

        return (bodyMaxX, finMinX, bodyMaxY, finMinY, finMinX - bodyMaxX, finMinY - bodyMaxY);
    }
}
