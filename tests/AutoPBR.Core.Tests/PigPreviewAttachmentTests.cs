using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class PigPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string PigJvm = "net.minecraft.client.model.animal.pig.PigModel";
    private const string PigTexturePath = "assets/minecraft/textures/entity/pig/pig_temperate.png";
    private const string CowTexturePath = "assets/minecraft/textures/entity/cow/cow_temperate.png";

    private readonly ITestOutputHelper _output;

    public PigPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Adult_pig_runtime_mesh_legs_attach_to_body_shell_in_preview_space()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            PigTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(PigJvm, provenance.Detail, StringComparison.Ordinal);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PigJvm });

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [PigTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [PigTexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var (bodyMaxY, legMinY, hullGap) = MeasureBodyLegPreviewHullGap(gpuVerts, partIds);
        _output.WriteLine($"pig preview bodyMaxY={bodyMaxY:F4} legMinY={legMinY:F4} hullGap={hullGap:F4}");

        var (legTopY, bodyUnderY, modelGap) = MeasureModelSpaceLegBodyGap(bind, partIds);
        _output.WriteLine($"pig model legTopY={legTopY:F4} bodyUnderY={bodyUnderY:F4} modelGap={modelGap:F4}");

        foreach (var legId in new[] { "left_front_leg", "right_front_leg", "left_hind_leg", "right_hind_leg" })
        {
            var local = MeasureLegAttachmentGap(bind, partIds, legId);
            _output.WriteLine($"{legId}: legTop={local.LegTopY:F4} bodyNear={local.BodyNearY:F4} gap={local.Gap:F4}");
            Assert.True(local.Gap < 0.15f, $"{legId} detached from body (gap={local.Gap:F3})");
        }
    }

    [Fact]
    public void Pig_cpu_rebake_leg_top_face_x_extent_vs_body_at_joint_y()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(PigTexturePath, Profile26, 0f, 0f, out var merged, out _));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = PigTexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = [PigTexturePath],
        };
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake, [CreatePigMaterial()], 0f, out var verts, out _, out _, applyGeometryIrSetupAnimMotion: false));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        foreach (var legId in new[] { "left_front_leg", "right_front_leg" })
        {
            var legTopY = MeasurePartMaxY(verts!, stride, merged, rebake.ElementPartIds!, legId);
            var legTopX = MeasureVertsNearY(verts!, stride, merged, rebake.ElementPartIds!, legId, legTopY, yTol: 0.01f);
            var legTopZ = MeasureVertsNearYZ(verts!, stride, merged, rebake.ElementPartIds!, legId, legTopY, yTol: 0.01f, axisZ: true);
            var bodyNearX = MeasureVertsNearY(verts!, stride, merged, rebake.ElementPartIds!, "body", legTopY, yTol: 0.06f);
            var bodyNearZ = MeasureVertsNearYZ(verts!, stride, merged, rebake.ElementPartIds!, "body", legTopY, yTol: 0.06f, axisZ: true);
            var bodyNearXAtLegZ = MeasureBodyVertsNearYAndZ(
                verts!, stride, merged, rebake.ElementPartIds!, legTopY, legTopZ.Min, legTopZ.Max, yTol: 0.06f);
            var outwardGapX = legId.StartsWith("left_", StringComparison.Ordinal)
                ? legTopX.Min - bodyNearX.Min
                : legTopX.Max - bodyNearX.Max;
            var outwardGapXAtLegZ = legId.StartsWith("left_", StringComparison.Ordinal)
                ? legTopX.Min - bodyNearXAtLegZ.Min
                : legTopX.Max - bodyNearXAtLegZ.Max;
            var frontGapZ = legTopZ.Min - bodyNearZ.Min;
            _output.WriteLine(
                $"{legId} topY={legTopY:F4} legTopX=[{legTopX.Min:F4},{legTopX.Max:F4}] bodyX@Y=[{bodyNearX.Min:F4},{bodyNearX.Max:F4}] " +
                $"bodyX@Y+legZ=[{bodyNearXAtLegZ.Min:F4},{bodyNearXAtLegZ.Max:F4}] outwardGapX={outwardGapX:F4} outwardGapX@legZ={outwardGapXAtLegZ:F4} " +
                $"legTopZ=[{legTopZ.Min:F4},{legTopZ.Max:F4}] bodyZ@Y=[{bodyNearZ.Min:F4},{bodyNearZ.Max:F4}] frontGapZ={frontGapZ:F4}");
            Assert.InRange(outwardGapX, -0.05f, 0.05f);
            // Front legs can expose ~1 preview texel of leg-top Z past the rotated body sheet at joint Y (vanilla sheet contour).
            Assert.InRange(frontGapZ, -0.05f, 0.08f);
        }
    }

    private static (float Min, float Max) MeasureBodyVertsNearYAndZ(
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds,
        float y,
        float zMin,
        float zMax,
        float yTol)
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            if (partIds[e].Contains("body", StringComparison.Ordinal))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vi + v) * stride;
                    if (MathF.Abs(verts[i + 1] - y) > yTol || verts[i + 2] < zMin - 0.01f || verts[i + 2] > zMax + 0.01f)
                    {
                        continue;
                    }

                    minX = MathF.Min(minX, verts[i]);
                    maxX = MathF.Max(maxX, verts[i]);
                }
            }

            vi += vertCount;
        }

        return (minX, maxX);
    }

    private static (float Min, float Max) MeasureVertsNearYZ(
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds,
        string partId,
        float y,
        float yTol,
        bool axisZ)
    {
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            if (string.Equals(partIds[e], partId, StringComparison.Ordinal) ||
                (partId.Contains("body", StringComparison.Ordinal) && partIds[e].Contains("body", StringComparison.Ordinal)))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vi + v) * stride;
                    if (MathF.Abs(verts[i + 1] - y) > yTol)
                    {
                        continue;
                    }

                    var val = axisZ ? verts[i + 2] : verts[i];
                    minV = MathF.Min(minV, val);
                    maxV = MathF.Max(maxV, val);
                }
            }

            vi += vertCount;
        }

        return (minV, maxV);
    }

    private static float MeasurePartMaxY(
        float[] verts, int stride, MergedJavaBlockModel mesh, string[] partIds, string partId)
    {
        var maxY = float.NegativeInfinity;
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            if (string.Equals(partIds[e], partId, StringComparison.Ordinal))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    maxY = MathF.Max(maxY, verts[(vi + v) * stride + 1]);
                }
            }

            vi += vertCount;
        }

        return maxY;
    }

    private static (float Min, float Max) MeasureVertsNearY(
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds,
        string partId,
        float y,
        float yTol)
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            if (string.Equals(partIds[e], partId, StringComparison.Ordinal) ||
                (partId.Contains("body", StringComparison.Ordinal) && partIds[e].Contains("body", StringComparison.Ordinal)))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vi + v) * stride;
                    if (MathF.Abs(verts[i + 1] - y) > yTol)
                    {
                        continue;
                    }

                    minX = MathF.Min(minX, verts[i]);
                    maxX = MathF.Max(maxX, verts[i]);
                }
            }

            vi += vertCount;
        }

        return (minX, maxX);
    }

    [Fact]
    public void Pig_cpu_rebake_preview_left_leg_inner_x_aligns_with_body_outer_shell()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            PigTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var merged,
            out _,
            applyGeometryIrSetupAnimMotion: false));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = PigTexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = [PigTexturePath],
        };
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            [CreatePigMaterial()],
            animationTimeSeconds: 0f,
            out var verts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.NotNull(rebake.ElementPartIds);

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var bounds = MeasureCpuPreviewBoundsByPart(
            verts!,
            stride,
            merged,
            rebake.ElementPartIds!);
        Assert.True(bounds.TryGetValue("body", out var body));
        foreach (var (partId, b) in bounds.OrderBy(kv => kv.Key))
        {
            _output.WriteLine(
                $"{partId}: X=[{b.Min.X:F4},{b.Max.X:F4}] Y=[{b.Min.Y:F4},{b.Max.Y:F4}] Z=[{b.Min.Z:F4},{b.Max.Z:F4}]");
        }

        foreach (var legId in new[] { "left_front_leg", "left_hind_leg", "right_front_leg", "right_hind_leg" })
        {
            Assert.True(bounds.TryGetValue(legId, out var leg));
            var isLeft = legId.StartsWith("left_", StringComparison.Ordinal);
            var bodyEdge = isLeft ? body.Min.X : body.Max.X;
            var legEdge = isLeft ? leg.Min.X : leg.Max.X;
            var outerDelta = legEdge - bodyEdge;
            var innerDelta = isLeft ? leg.Max.X - body.Min.X : leg.Min.X - body.Max.X;
            _output.WriteLine(
                $"{legId}: bodyEdge={bodyEdge:F4} legEdge={legEdge:F4} outerDelta={outerDelta:F4} innerDelta={innerDelta:F4}");
            Assert.InRange(outerDelta, -0.05f, 0.05f);
        }
    }

    [Fact]
    public void Pig_cpu_rebake_preview_legs_attach_to_body_shell()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            PigTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var merged,
            out _,
            applyGeometryIrSetupAnimMotion: false));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = PigTexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = [PigTexturePath],
        };
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            [CreatePigMaterial()],
            animationTimeSeconds: 0f,
            out var verts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.NotNull(rebake.ElementPartIds);

        var bounds = MeasureCpuPreviewBoundsByPart(
            verts!,
            MinecraftModelBaker.FloatsPerVertex,
            merged,
            rebake.ElementPartIds!);
        Assert.True(bounds.TryGetValue("body", out var body), "missing body bounds");

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        foreach (var legId in new[] { "left_front_leg", "right_front_leg", "left_hind_leg", "right_hind_leg" })
        {
            Assert.True(bounds.TryGetValue(legId, out var leg), $"missing {legId} bounds");
            var gap = PartPreviewBounds.SeparatedDistance(body, leg);
            var localGap = MeasureLocalizedLegTopBodyUndersideGap(verts!, stride, merged, rebake.ElementPartIds!, leg);
            _output.WriteLine(
                $"{legId}: center=({leg.Center.X:F4},{leg.Center.Z:F4}) aabbGap={gap:F4} localJointGap={localGap:F4} " +
                $"bodyY=[{body.Min.Y:F4},{body.Max.Y:F4}] legY=[{leg.Min.Y:F4},{leg.Max.Y:F4}]");
            Assert.True(gap <= 0.05f, $"{legId} detached in CPU preview rebake (gap={gap:F3})");
            Assert.True(localGap <= 0.06f, $"{legId} localized joint gap (gap={localGap:F3})");
        }
    }

    [Fact]
    public void Pig_body_bind_pose_uses_translate_then_rotate_not_part_pose_er_times_t()
    {
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json")));
        Assert.True(TryFindPartPose(
            GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement),
            "body",
            out var bodyPose));
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(bodyPose, Matrix4x4.Identity, out var irBind));

        var separated = SeparatedTranslateRotateBlock(
            Matrix4x4.CreateTranslation(0f, 11f, 2f),
            Matrix4x4.CreateRotationX(MathF.PI / 2f));

        var corner = new Vector3(-5f, -10f, -7f);
        var irWorld = Vector3.Transform(corner, irBind);
        var sepWorld = Vector3.Transform(corner, separated);
        _output.WriteLine($"ir={irWorld} separated={sepWorld} dist={Vector3.Distance(irWorld, sepWorld):F4}");
        Assert.True(Vector3.Distance(irWorld, sepWorld) <= 0.05f);
    }

    private static Matrix4x4 SeparatedTranslateRotateBlock(Matrix4x4 translation, Matrix4x4 rotation) =>
        new(
            rotation.M11, rotation.M12, rotation.M13, rotation.M14,
            rotation.M21, rotation.M22, rotation.M23, rotation.M24,
            rotation.M31, rotation.M32, rotation.M33, rotation.M34,
            translation.M41, translation.M42, translation.M43, translation.M44);

    [Fact]
    public void Pig_bind_pose_bake_preserves_right_hind_leg_outer_x_alignment()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(PigTexturePath, Profile26, 0f, 0f, out var merged, out _));

        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json")));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement),
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PigJvm });

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [PigTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [PigTexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var body = MeasurePartAxisRangeByBoneIndex(verts, stride, partIds, "body");
        var leg = MeasurePartAxisRangeByBoneIndex(verts, stride, partIds, "right_hind_leg");
        var outerDelta = leg.MaxX - body.MaxX;
        _output.WriteLine(
            $"bind bodyX=[{body.MinX:F4},{body.MaxX:F4}] legX=[{leg.MinX:F4},{leg.MaxX:F4}] outerDelta={outerDelta:F4}");
        Assert.InRange(outerDelta, -0.05f, 0.05f);
    }

    private static (float MinX, float MaxX, float MinY, float MaxY) MeasurePartAxisRangeByBoneIndex(
        float[] verts,
        int stride,
        List<string> partIds,
        string partId)
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        for (var i = 0; i + stride - 1 < verts.Length; i += stride)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(verts[i + 12]);
            if (bi < 0 || bi >= partIds.Count || !string.Equals(partIds[bi], partId, StringComparison.Ordinal))
            {
                continue;
            }

            var p = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
            minX = MathF.Min(minX, p.X);
            maxX = MathF.Max(maxX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxY = MathF.Max(maxY, p.Y);
        }

        return (minX, maxX, minY, maxY);
    }

    [Fact]
    public void Pig_runtime_mesh_hind_leg_outer_shell_aligns_with_body_after_ler()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(PigTexturePath, Profile26, 0f, 0f, out var mesh, out _));

        var body = FindElement(mesh, "body");
        var leg = FindElement(mesh, "right_hind_leg");
        Assert.NotNull(body);
        Assert.NotNull(leg);

        TransformElementCorners(body!, out var bodyMin, out var bodyMax);
        TransformElementCorners(leg!, out var legMin, out var legMax);
        var outerGap = legMax.X - bodyMax.X;
        _output.WriteLine(
            $"ler mesh bodyX=[{bodyMin.X:F3},{bodyMax.X:F3}] legX=[{legMin.X:F3},{legMax.X:F3}] outerGap={outerGap:F3}");
        Assert.InRange(outerGap, -0.05f, 0.05f);
    }

    [Fact]
    public void Pig_model_space_mesh_corners_match_java_reference_bind_pose()
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "tools", "MinecraftGeometryReference", "reference-output", $"{PigJvm}.json")));
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        var mesh = EntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "entity/pig/pig", PigJvm, 64, 64, repaired, out var err);
        Assert.NotNull(mesh);
        Assert.Null(err);

        foreach (var (partId, local) in new (string, Vector3)[]
                 {
                     ("body", new Vector3(-5f, -10f, -7f)),
                     ("right_hind_leg", new Vector3(-2f, 0f, -2f)),
                     ("right_hind_leg", new Vector3(-2f, 6f, -2f)),
                     ("right_front_leg", new Vector3(-2f, 0f, -2f)),
                 })
        {
            Assert.True(TryReferenceRenderAffine(reference.RootElement, partId, out var refAffine));
            var el = FindElement(mesh!, partId);
            Assert.NotNull(el);
            var refWorld = Vector3.Transform(local, refAffine);
            var meshWorld = Vector3.Transform(local, el!.LocalToParent);
            var dist = Vector3.Distance(refWorld, meshWorld);
            _output.WriteLine($"{partId}@{local}: ref={refWorld} mesh={meshWorld} dist={dist:F4}");
            Assert.True(dist <= 0.08f, $"{partId} corner dist={dist:F3}");
        }

        TransformElementCorners(FindElement(mesh!, "body")!, out var bodyMin, out _);
        TransformElementCorners(FindElement(mesh!, "right_hind_leg")!, out var legMin, out _);
        Assert.InRange(legMin.X - bodyMin.X, -0.05f, 0.05f);
    }

    [Fact]
    public void Pig_ler_fold_preserves_rotated_body_to_leg_outer_shell_alignment()
    {
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        Assert.True(TryFindPartPose(repaired, "body", out var bodyPose));
        Assert.True(TryFindPartPose(repaired, "right_front_leg", out var legPose));
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(bodyPose, Matrix4x4.Identity, out var bodyBind));
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(legPose, Matrix4x4.Identity, out var legBind));

        var bodyCorner = new Vector3(-5f, -10f, -7f);
        var legCorner = new Vector3(-2f, 0f, -2f);
        var bodyLer = EntityModelRuntime.ApplyLivingEntityRendererColumnRootScale(bodyBind);
        var legLer = EntityModelRuntime.ApplyLivingEntityRendererColumnRootScale(legBind);
        var outerGap = Vector3.Transform(legCorner, legLer).X - Vector3.Transform(bodyCorner, bodyLer).X;
        _output.WriteLine($"ler outerGap={outerGap:F3}");
        Assert.InRange(outerGap, -0.05f, 0.05f);
    }

    private static bool TryFindPartPose(JsonElement geometryRoot, string partId, out JsonElement pose)
    {
        pose = default;
        if (!geometryRoot.TryGetProperty("roots", out var roots))
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (WalkPartPose(root, partId, out pose))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WalkPartPose(JsonElement node, string partId, out JsonElement pose)
    {
        pose = default;
        if (node.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), partId, StringComparison.Ordinal) &&
            node.TryGetProperty("pose", out pose))
        {
            return true;
        }

        if (node.TryGetProperty("children", out var children))
        {
            foreach (var ch in children.EnumerateArray())
            {
                if (WalkPartPose(ch, partId, out pose))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ModelElement? FindElement(MergedJavaBlockModel mesh, string partId)
    {
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PigJvm });
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                return mesh.Elements[i];
            }
        }

        return null;
    }

    private static void TransformElementCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (el.From[0], el.From[1], el.From[2]),
            (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]),
            (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]),
            (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]),
            (el.To[0], el.To[1], el.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }

    private static bool TryReferenceRenderAffine(JsonElement referenceRoot, string partId, out Matrix4x4 matrix)
    {
        matrix = Matrix4x4.Identity;
        if (!referenceRoot.TryGetProperty("renderPartAffines", out var affines))
        {
            return false;
        }

        foreach (var entry in affines.EnumerateArray())
        {
            if (!string.Equals(entry.GetProperty("id").GetString(), partId, StringComparison.Ordinal))
            {
                continue;
            }

            var rows = entry.GetProperty("matrixRowMajor");
            matrix = MatrixFromReferenceRows(rows);
            matrix = EntityModelRuntime.BlockRowAffineToTexel(matrix);
            return true;
        }

        return false;
    }

    private static Matrix4x4 MatrixFromReferenceRows(JsonElement rows)
    {
        float At(int r, int c) => (float)rows[r][c].GetDouble();
        return new Matrix4x4(
            At(0, 0), At(0, 1), At(0, 2), At(0, 3),
            At(1, 0), At(1, 1), At(1, 2), At(1, 3),
            At(2, 0), At(2, 1), At(2, 2), At(2, 3),
            At(3, 0), At(3, 1), At(3, 2), At(3, 3));
    }

    [Fact]
    public void Cow_runtime_mesh_leg_attachment_matches_pig_policy()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            CowTexturePath, Profile26, 0f, 0f, out var bind, out _, applyGeometryIrSetupAnimMotion: false));

        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.cow.CowModel.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(
            "net.minecraft.client.model.animal.cow.CowModel", shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with
            {
                OfficialJvmName = "net.minecraft.client.model.animal.cow.CowModel"
            });

        var gap = MeasureLegAttachmentGap(bind, partIds, "left_front_leg");
        _output.WriteLine($"cow left_front_leg gap={gap.Gap:F4}");
        Assert.True(gap.Gap < 0.15f);
    }

    private static (float LegTopY, float BodyNearY, float Gap) MeasureLegAttachmentGap(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        string legId)
    {
        Vector3? legPivot = null;
        var legTopY = float.NegativeInfinity;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (!string.Equals(partIds[i], legId, StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[i];
            legPivot ??= new Vector3(el.LocalToParent.M41, el.LocalToParent.M42, el.LocalToParent.M43);
            ReadOnlySpan<(float x, float y, float z)> corners =
            [
                (el.From[0], el.From[1], el.From[2]),
                (el.To[0], el.From[1], el.From[2]),
                (el.From[0], el.To[1], el.From[2]),
                (el.To[0], el.To[1], el.From[2]),
                (el.From[0], el.From[1], el.To[2]),
                (el.To[0], el.From[1], el.To[2]),
                (el.From[0], el.To[1], el.To[2]),
                (el.To[0], el.To[1], el.To[2]),
            ];
            foreach (var (x, y, z) in corners)
            {
                var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
                legTopY = MathF.Max(legTopY, w.Y);
            }
        }

        if (legPivot is null)
        {
            return (float.NaN, float.NaN, float.PositiveInfinity);
        }

        var bodyNearY = float.PositiveInfinity;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (!partIds[i].Contains("body", StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[i];
            ReadOnlySpan<(float x, float y, float z)> corners =
            [
                (el.From[0], el.From[1], el.From[2]),
                (el.To[0], el.From[1], el.From[2]),
                (el.From[0], el.To[1], el.From[2]),
                (el.To[0], el.To[1], el.From[2]),
                (el.From[0], el.From[1], el.To[2]),
                (el.To[0], el.From[1], el.To[2]),
                (el.From[0], el.To[1], el.To[2]),
                (el.To[0], el.To[1], el.To[2]),
            ];
            foreach (var (x, y, z) in corners)
            {
                var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
                if (MathF.Abs(w.X - legPivot.Value.X) > 3.5f || MathF.Abs(w.Z - legPivot.Value.Z) > 3.5f)
                {
                    continue;
                }

                bodyNearY = MathF.Min(bodyNearY, w.Y);
            }
        }

        var gap = bodyNearY < float.PositiveInfinity ? bodyNearY - legTopY : float.PositiveInfinity;
        return (legTopY, bodyNearY, gap);
    }

    private static (float LegTopY, float BodyUnderY, float Gap) MeasureModelSpaceLegBodyGap(
        MergedJavaBlockModel mesh,
        List<string> partIds)
    {
        var legTopY = float.NegativeInfinity;
        var bodyUnderY = float.PositiveInfinity;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var id = partIds[i];
            var el = mesh.Elements[i];
            ReadOnlySpan<(float x, float y, float z)> corners =
            [
                (el.From[0], el.From[1], el.From[2]),
                (el.To[0], el.From[1], el.From[2]),
                (el.From[0], el.To[1], el.From[2]),
                (el.To[0], el.To[1], el.From[2]),
                (el.From[0], el.From[1], el.To[2]),
                (el.To[0], el.From[1], el.To[2]),
                (el.From[0], el.To[1], el.To[2]),
                (el.To[0], el.To[1], el.To[2]),
            ];
            foreach (var (x, y, z) in corners)
            {
                var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
                if (id.Contains("leg", StringComparison.Ordinal))
                {
                    legTopY = MathF.Max(legTopY, w.Y);
                }
                else if (id.Contains("body", StringComparison.Ordinal))
                {
                    bodyUnderY = MathF.Min(bodyUnderY, w.Y);
                }
            }
        }

        var gap = legTopY > float.NegativeInfinity && bodyUnderY < float.PositiveInfinity
            ? bodyUnderY - legTopY
            : float.PositiveInfinity;
        return (legTopY, bodyUnderY, gap);
    }

    private static (float BodyMaxY, float LegMinY, float HullGap) MeasureBodyLegPreviewHullGap(
        ReadOnlySpan<float> gpuVerts,
        List<string> partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var bodyMaxY = float.NegativeInfinity;
        var legMinY = float.PositiveInfinity;
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
                bodyMaxY = MathF.Max(bodyMaxY, preview.Y);
            }
            else if (id.Contains("leg", StringComparison.Ordinal))
            {
                legMinY = MathF.Min(legMinY, preview.Y);
            }
        }

        return (bodyMaxY, legMinY, bodyMaxY < float.PositiveInfinity && legMinY < float.NegativeInfinity
            ? legMinY - bodyMaxY
            : float.PositiveInfinity);
    }

    private static float MeasureLocalizedLegTopBodyUndersideGap(
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds,
        PartPreviewBounds legBounds) =>
        MeasureLocalizedLegTopBodyUndersideGapAt(verts, stride, mesh, partIds, legBounds, radius: 1.25f);

    private static float MeasureLocalizedLegTopBodyUndersideGapAt(
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds,
        PartPreviewBounds legBounds,
        float radius)
    {
        var legTopY = legBounds.Max.Y;
        var pivotX = legBounds.Center.X;
        var pivotZ = legBounds.Center.Z;
        var bodyUnderY = float.PositiveInfinity;
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            if (partIds[e].Contains("body", StringComparison.Ordinal))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = vi * stride;
                    var p = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                    if (MathF.Abs(p.X - pivotX) > radius || MathF.Abs(p.Z - pivotZ) > radius)
                    {
                        continue;
                    }

                    bodyUnderY = MathF.Min(bodyUnderY, p.Y);
                }
            }

            vi += vertCount;
        }

        return bodyUnderY < float.PositiveInfinity ? bodyUnderY - legTopY : float.PositiveInfinity;
    }

    private static int CountElementFaceVerts(ModelElement el) =>
        (el.Faces is { Count: > 0 } faces ? faces.Count : 6) * 4;

    private static PreviewTextureMaps CreatePigMaterial() => new()
    {
        Width = 64,
        Height = 64,
        DiffuseRgba = new byte[64 * 64 * 4],
        NormalRgba = new byte[64 * 64 * 4],
        SpecularRgba = new byte[64 * 64 * 4],
        HeightRgba = new byte[64 * 64 * 4],
    };

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
        float[] verts,
        int stride,
        MergedJavaBlockModel mesh,
        string[] partIds)
    {
        var result = new Dictionary<string, PartPreviewBounds>(StringComparer.Ordinal);
        var vi = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var partId = partIds[e];
            var vertCount = CountElementFaceVerts(mesh.Elements[e]);
            PartPreviewBounds? bounds = null;
            for (var v = 0; v < vertCount; v++)
            {
                var i = vi * stride;
                var p = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                bounds = bounds is { } existing ? existing.Include(p) : PartPreviewBounds.FromPoint(p);
                vi++;
            }

            if (bounds is { } b)
            {
                result[partId] = result.TryGetValue(partId, out var prior) ? prior.Union(b) : b;
            }
        }

        return result;
    }
}
