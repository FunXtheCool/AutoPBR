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
    public void Runtime_cpu_rebake_preview_vbo_fin_vertices_match_hand_builder()
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
            out var runtimeMesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        var rebake = CreateDolphinRebakeContext(idlePhase01: 0f);
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            CreateDolphinMaterialSet(),
            animationTimeSeconds: 0f,
            out var runtimeVerts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.NotNull(rebake.ElementPartIds);

        var runtimeBounds = MeasureCpuPreviewBoundsByPart(
            runtimeVerts!,
            MinecraftModelBaker.FloatsPerVertex,
            runtimeMesh!,
            rebake.ElementPartIds!);

        var hand = CleanRoomEntityModelRuntime.BuildDolphinHandMeshForTests("entity/dolphin/dolphin", Profile26);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(hand, "minecraft", pathToIdx, texSizes, out var handVerts, out _, out _));

        var handPartIds = DolphinHandPartIds();
        EntityPreviewPlacement.ApplyToPreviewVertices(
            handVerts,
            MinecraftModelBaker.FloatsPerVertex,
            handPartIds);
        var handBounds = MeasureCpuPreviewBoundsByPart(
            handVerts,
            MinecraftModelBaker.FloatsPerVertex,
            hand,
            handPartIds);

        foreach (var finId in new[] { "back_fin", "left_fin", "right_fin" })
        {
            Assert.True(runtimeBounds.TryGetValue(finId, out var runtimeFin), $"runtime missing {finId}");
            Assert.True(handBounds.TryGetValue(finId, out var handFin), $"hand missing {finId}");
            Assert.True(runtimeBounds.TryGetValue("body", out var runtimeBody), "runtime missing body");
            Assert.True(handBounds.TryGetValue("body", out var handBody), "hand missing body");

            var centerGap = Vector3.Distance(runtimeFin.Center, handFin.Center);
            var runtimeBodyGap = PartPreviewBounds.SeparatedDistance(runtimeBody, runtimeFin);
            var handBodyGap = PartPreviewBounds.SeparatedDistance(handBody, handFin);
            _output.WriteLine(
                $"{finId}: runtimeCenter={runtimeFin.Center} handCenter={handFin.Center} " +
                $"centerGap={centerGap:F4} runtimeBodyGap={runtimeBodyGap:F4} handBodyGap={handBodyGap:F4}");

            Assert.True(centerGap <= 0.08f, $"{finId} CPU preview VBO center diverged from hand builder (gap={centerGap:F3})");
            Assert.True(runtimeBodyGap <= 0.05f,
                $"{finId} CPU preview VBO should touch the body hull (gap={runtimeBodyGap:F3})");
            Assert.True(handBodyGap <= 0.05f,
                $"{finId} hand preview VBO should touch the body hull (gap={handBodyGap:F3})");
            Assert.True(Math.Abs(runtimeBodyGap - handBodyGap) <= 0.08f,
                $"{finId} CPU preview VBO body separation diverged from hand builder (runtime={runtimeBodyGap:F3}, hand={handBodyGap:F3})");
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
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });

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
            repaired, GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });

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
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });

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
    public void Dolphin_emit_uses_model_part_block_stack_not_column_part_pose()
    {
        var opts = GeometryIrMeshEmitOptions.ForParity(64, 64) with
        {
            OfficialJvmName = "net.minecraft.client.model.animal.dolphin.DolphinModel",
        };
        Assert.False(opts.ResolveUseColumnTranslationTimesRotationPartPose());
    }

    [Fact]
    public void Dolphin_block_stack_compose_attaches_fins_to_hand_builder()
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
            Assert.True(gap <= 0.08f, $"{finId} block-stack compose should match hand builder (gap={gap:F3})");
        }
    }

    [Fact]
    public void Legacy_dolphin_pose_lift_corruption_is_rejected_for_parity_catalog_lookup()
    {
        var legacyProfile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(legacyProfile, Jvm, out var legacyRoot));
        Assert.Equal(
            GeometryIrLiftPolicyDecision.RejectForParity,
            GeometryIrLiftPolicy.EvaluateDocument(legacyRoot));

        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedForParityCatalog(legacyProfile, Jvm, out var resolvedRoot));
        Assert.Equal("26.1.2", resolvedRoot.GetProperty("versionLabel").GetString());
    }

    [Fact]
    public void Runtime_mesh_fin_pose_ignores_global_legacy_pose_debug_switch()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var oldLegacy = EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose;
        try
        {
            EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = true;
            var runtime = EntityModelRuntimeFactory.Create();
            Assert.True(runtime.TryBuildStaticMesh(
                TexturePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out var provenance,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
            var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
                repaired,
                GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm });
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
                var handOrigin = Vector3.Transform(Vector3.Zero, hand.Elements[handIdx].LocalToParent);
                var meshOrigin = Vector3.Transform(Vector3.Zero, meshWorld.Value);
                var gap = Vector3.Distance(handOrigin, meshOrigin);
                _output.WriteLine($"{finId}: hand={handOrigin} mesh={meshOrigin} gap={gap:F3}");
                Assert.True(gap <= 0.08f, $"{finId} should ignore global legacy pose debug switch (gap={gap:F3})");
            }
        }
        finally
        {
            EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = oldLegacy;
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

    private static EntityEmulatedPreviewRebakeContext CreateDolphinRebakeContext(float idlePhase01) =>
        new()
        {
            PackZipPath = "test.zip",
            AssetArchivePath = TexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idlePhase01,
            OrderedTextureZipPaths = [TexturePath],
        };

    private static PreviewTextureMaps[] CreateDolphinMaterialSet() =>
        [
            new PreviewTextureMaps
            {
                Width = 64,
                Height = 64,
                DiffuseRgba = new byte[64 * 64 * 4],
                NormalRgba = new byte[64 * 64 * 4],
                SpecularRgba = new byte[64 * 64 * 4],
                HeightRgba = new byte[64 * 64 * 4],
            }
        ];

    private static string[] DolphinHandPartIds() =>
        ["body", "back_fin", "left_fin", "right_fin", "tail", "tail_fin", "head", "nose"];

    private readonly record struct PartPreviewBounds(Vector3 Min, Vector3 Max)
    {
        public Vector3 Center => (Min + Max) * 0.5f;

        public static PartPreviewBounds FromPoint(Vector3 p) => new(p, p);

        public PartPreviewBounds Include(Vector3 p) => new(Vector3.Min(Min, p), Vector3.Max(Max, p));

        public PartPreviewBounds Union(PartPreviewBounds other) =>
            new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

        public static float SeparatedDistance(PartPreviewBounds a, PartPreviewBounds b)
        {
            var dx = MathF.Max(0f, MathF.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
            var dy = MathF.Max(0f, MathF.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
            var dz = MathF.Max(0f, MathF.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    private static Dictionary<string, PartPreviewBounds> MeasureCpuPreviewBoundsByPart(
        ReadOnlySpan<float> vertices,
        int stride,
        MergedJavaBlockModel model,
        string[] partIds)
    {
        var result = new Dictionary<string, PartPreviewBounds>(StringComparer.Ordinal);
        var floatOffset = 0;
        var elementCount = Math.Min(model.Elements.Count, partIds.Length);
        for (var ei = 0; ei < elementCount; ei++)
        {
            var vertexCount = model.Elements[ei].Faces.Count * 4;
            if (vertexCount <= 0)
            {
                continue;
            }

            PartPreviewBounds? bounds = null;
            for (var vi = 0; vi < vertexCount && floatOffset + stride - 1 < vertices.Length; vi++, floatOffset += stride)
            {
                var p = new Vector3(vertices[floatOffset], vertices[floatOffset + 1], vertices[floatOffset + 2]);
                bounds = bounds is { } existing ? existing.Include(p) : PartPreviewBounds.FromPoint(p);
            }

            if (bounds is not { } measured)
            {
                continue;
            }

            var id = partIds[ei];
            result[id] = result.TryGetValue(id, out var aggregate)
                ? aggregate.Union(measured)
                : measured;
        }

        return result;
    }

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
