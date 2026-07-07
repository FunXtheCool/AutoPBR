using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class AxolotlPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string Jvm = "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel";
    private const string TexturePath = "assets/minecraft/textures/entity/axolotl/axolotl_blue.png";

    private readonly ITestOutputHelper _output;

    public AxolotlPreviewAttachmentTests(ITestOutputHelper output) => _output = output;


    [Fact]
    public void Runtime_mesh_gill_face_uv_corners_match_vanilla_uv_span_layout()
    {
        AssertGillFaceUvCornersMatchVanilla(TexturePath, Jvm);
    }

    [Fact]
    public void Baby_runtime_mesh_gill_face_uv_corners_match_vanilla_uv_span_layout()
    {
        const string babyTexture = "assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png";
        const string babyJvm = "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel";
        AssertGillFaceUvCornersMatchVanilla(babyTexture, babyJvm);
    }

    private void AssertGillFaceUvCornersMatchVanilla(string texturePath, string jvm)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath, Profile26, 0f, 0f, out var mesh, out _, applyGeometryIrSetupAnimMotion: false));

        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs", "generated", "geometry", "26.1.2", $"{jvm}.json")));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired, GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm });

        var expected = BuildGillTexCropExpectedFromShard(repaired);

        foreach (var (gillId, faces) in expected)
        {
            ModelElement? irEl = null;
            for (var i = 0; i < mesh!.Elements.Count; i++)
            {
                if (string.Equals(partIds[i], gillId, StringComparison.Ordinal))
                {
                    irEl = mesh.Elements[i];
                    break;
                }
            }

            Assert.NotNull(irEl);
            foreach (var (faceName, expUv) in faces)
            {
                Assert.True(irEl!.Faces.TryGetValue(faceName, out var irFace), $"{gillId}.{faceName}");
                Assert.NotNull(irFace.Uv);
                var irUv = irFace.Uv!;
                _output.WriteLine($"{gillId}.{faceName} expected=[{expUv[0]},{expUv[1]},{expUv[2]},{expUv[3]}] ir=[{irUv[0]},{irUv[1]},{irUv[2]},{irUv[3]}]");
                for (var c = 0; c < 4; c++)
                {
                    Assert.True(MathF.Abs(expUv[c] - irUv[c]) <= 0.05f,
                        $"{gillId}.{faceName}[{c}]: expected={expUv[c]} ir={irUv[c]}");
                }
            }
        }
    }

    private static Dictionary<string, Dictionary<string, float[]>> BuildGillTexCropExpectedFromShard(JsonElement geometryRoot)
    {
        var expected = new Dictionary<string, Dictionary<string, float[]>>(StringComparer.Ordinal);
        WalkGillParts(geometryRoot, part =>
        {
            if (!part.TryGetProperty("id", out var idEl) ||
                idEl.GetString() is not { } partId ||
                !partId.EndsWith("_gills", StringComparison.Ordinal))
            {
                return;
            }

            if (!part.TryGetProperty("cuboids", out var cuboids) || cuboids.GetArrayLength() == 0)
            {
                return;
            }

            var cuboid = cuboids[0];
            if (!cuboid.TryGetProperty("uvOrigin", out var uv) || uv.GetArrayLength() < 2 ||
                !GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var faceMask) ||
                !IsNorthSouthFaceMaskOnly(faceMask))
            {
                return;
            }

            var texU = uv[0].GetInt32();
            var texV = uv[1].GetInt32();
            var w = ResolveGillSheetWidth(cuboid);
            var h = ResolveGillSheetHeight(cuboid);
            var (north, south) = (
                EntityCuboidJavaUvConvention.GetUvRect(
                    EntityCuboidJavaUvConvention.JavaDirection.North, texU, texV, w, h, 0),
                EntityCuboidJavaUvConvention.GetUvRect(
                    EntityCuboidJavaUvConvention.JavaDirection.South, texU, texV, w, h, 0));
            expected[partId] = new Dictionary<string, float[]>(StringComparer.Ordinal)
            {
                ["north"] = north,
                ["south"] = south,
            };
        });

        return expected;
    }

    private static void WalkGillParts(JsonElement node, Action<JsonElement> visit)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            visit(node);
            if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    WalkGillParts(child, visit);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                WalkGillParts(child, visit);
            }
        }
    }

    private static int ResolveGillSheetWidth(JsonElement cuboid)
    {
        if (GeometryIrCuboidMetadata.TryGetUvSpan(cuboid, out var spanW, out _, out _) && spanW > 0 &&
            (!cuboid.TryGetProperty("uvOrigin", out var uv) || spanW != uv[0].GetInt32()))
        {
            return spanW;
        }

        return LogicalAxisExtent(cuboid, "from", "to", 0);
    }

    private static int ResolveGillSheetHeight(JsonElement cuboid)
    {
        if (GeometryIrCuboidMetadata.TryGetUvSpan(cuboid, out _, out var spanH, out _) && spanH > 0 &&
            (!cuboid.TryGetProperty("uvOrigin", out var uv) || uv.GetArrayLength() < 2 || spanH != uv[1].GetInt32()))
        {
            return spanH;
        }

        return LogicalAxisExtent(cuboid, "from", "to", 1);
    }

    private static int LogicalAxisExtent(JsonElement cuboid, string fromKey, string toKey, int axis)
    {
        var from = cuboid.GetProperty(fromKey);
        var to = cuboid.GetProperty(toKey);
        return (int)MathF.Round(MathF.Abs((float)to[axis].GetDouble() - (float)from[axis].GetDouble()));
    }

    private static bool IsNorthSouthFaceMaskOnly(string[] faceMask)
    {
        if (faceMask.Length == 0)
        {
            return false;
        }

        foreach (var face in faceMask)
        {
            if (!face.Equals("north", StringComparison.OrdinalIgnoreCase) &&
                !face.Equals("south", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void Runtime_mesh_preview_world_matches_reference_java_for_body_and_legs()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{Jvm}.json");
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!File.Exists(refPath) ||
            !GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath,
            Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var opts = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = Jvm };

        var cmp = GeometryIrReferenceComparer.CompareReferenceJavaPreviewWorldToParityMesh(
            reference.RootElement, repaired, bind, opts, tolerance: 0.35);
        _output.WriteLine(cmp.Message ?? "match");
        Assert.True(cmp.IsMatch, cmp.Message);

        var (bodyY, legY, gap) = MeasureBodyLegPreviewGap(bind, repaired, opts);
        _output.WriteLine($"preview bodyY={bodyY:F4} legY={legY:F4} originGap={gap:F4}");
        // Java nests legs at body+1; 1.0 texel origin gap is correct (not a disconnect).
        Assert.InRange(gap, 0.95f, 1.05f);

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [TexturePath] = 0
        };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase)
        {
            [TexturePath] = (64, 64)
        };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, opts);
        var bones = EntityGpuShaderDiagnostics.BuildBindPoseBonePalette(bind);
        var snap = EntityGpuShaderDiagnostics.BuildRuntimeSnapshot(
            gpuVerts,
            MinecraftModelBaker.FloatsPerSkinnedVertex,
            partIds,
            bind.Elements.Count,
            boneFillOk: true,
            bonePaletteUploaded: false,
            uploadedGpuSkinning: 0,
            uploadedBoneCount: bind.Elements.Count,
            uploadedLiftY: 0f,
            uploadedBindMesh: 1,
            boneMatrices: bones,
            boneMatrixCount: bones.Length);
        _output.WriteLine(
            $"simBodyLegGap={snap.SimBodyLegGap:F4} simBodyY={snap.SimBodyCentroidY:F4} simLegY={snap.SimLegCentroidY:F4}");

        var (bodyMaxY, legMinY, hullGap) = MeasureBodyLegHullGap(gpuVerts, partIds);
        _output.WriteLine($"bodyMaxY={bodyMaxY:F4} legMinY={legMinY:F4} hullGap={hullGap:F4}");
        // Legs attach at body sides (leg origin body+1); hull overlap is expected at bind pose.
        Assert.True(hullGap < 0.05f, $"leg hull sits below body by {hullGap:F3} preview units");
    }

    [Fact]
    public void Catalog_emit_thickens_axolotl_degenerate_leg_sheets_for_viewport()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath,
            Profile26,
            idlePhase01: 0.3f,
            animationTimeSeconds: 0f,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(Jvm, shard.RootElement);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mesh, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(repaired, GeometryIrMeshEmitOptions.ForParity(64, 64));
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var legMinZ = float.PositiveInfinity;
        var legMaxZ = float.NegativeInfinity;
        var sawLeg = false;
        for (var v = 0; v < gpuVerts.Length / stride; v++)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(gpuVerts[(v * stride) + 12]);
            if (bi < 0 || bi >= partIds.Count || !partIds[bi].Contains("leg", StringComparison.Ordinal))
            {
                continue;
            }

            sawLeg = true;
            var p = new Vector3(gpuVerts[v * stride], gpuVerts[v * stride + 1], gpuVerts[v * stride + 2]);
            legMinZ = MathF.Min(legMinZ, p.Z);
            legMaxZ = MathF.Max(legMaxZ, p.Z);
        }

        Assert.True(sawLeg);
        Assert.True(legMaxZ - legMinZ > 1.5f, $"leg world Z span too thin ({legMaxZ - legMinZ:F3} texels)");
    }

    [Fact]
    public void Runtime_mesh_with_setup_anim_still_matches_bone_palette_at_bind_clock()
    {
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

    [Fact]
    public void Gpu_skinned_bind_pose_matches_cpu_rebake_at_idle_clock()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const float idle = 0.3f;
        const float anim = 1.25f;
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, idle, anim, out var animMesh, out _,
            applyGeometryIrSetupAnimMotion: true));
        Assert.True(runtime.TryBuildStaticMesh(TexturePath, Profile26, idle, 0f, out var bindMesh, out _,
            applyGeometryIrSetupAnimMotion: false));

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [TexturePath] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bindMesh, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));
        Assert.True(MinecraftModelBaker.TryBake(
            animMesh, "minecraft", pathToIdx, texSizes, out var cpuVerts, out _, out _));

        var scratch = new List<Matrix4x4>();
        Assert.True(runtime.TryFillBoneMatricesFast(TexturePath, Profile26, idle, anim, scratch, out var boneCount, null,
            applyGeometryIrSetupAnimMotion: true));
        var inv = new Matrix4x4[boneCount];
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(Matrix4x4.Invert(bindMesh.Elements[i].LocalToParent, out inv[i]));
        }

        const int skinStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        var maxDelta = 0f;
        for (var v = 0; v < gpuVerts.Length / skinStride; v++)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(gpuVerts[(v * skinStride) + 12]);
            if (bi < 0 || bi >= boneCount)
            {
                continue;
            }

            var pBind = new Vector3(
                gpuVerts[v * skinStride],
                gpuVerts[v * skinStride + 1],
                gpuVerts[v * skinStride + 2]);
            var skinned = Vector3.Transform(pBind, Matrix4x4.Multiply(inv[bi], scratch[bi]));
            var gpuPreview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(skinned);
            var cpuPreview = new Vector3(
                cpuVerts[v * cpuStride],
                cpuVerts[v * cpuStride + 1],
                cpuVerts[v * cpuStride + 2]);
            maxDelta = MathF.Max(maxDelta, Vector3.Distance(gpuPreview, cpuPreview));
        }

        _output.WriteLine($"anim skin maxDelta={maxDelta:F5}");
        Assert.True(maxDelta < 0.02f, $"GPU skinning diverges from CPU rebake by {maxDelta:F4}");
    }

    private static bool AllClose(in Matrix4x4 a, in Matrix4x4 b, float eps = 2e-4f) =>
        Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps && Math.Abs(a.M43 - b.M43) <= eps;

    private static (float BodyY, float LegY, float Gap) MeasureBodyLegPreviewGap(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        GeometryIrMeshEmitOptions options)
    {
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float? bodyY = null;
        float? legY = null;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var id = partIds[i];
            var cy = mesh.Elements[i].LocalToParent.M42;
            if (id.Contains("body", StringComparison.Ordinal) && !id.Contains("inner", StringComparison.Ordinal))
            {
                bodyY ??= cy;
            }
            else if (id.Contains("leg", StringComparison.Ordinal))
            {
                legY ??= cy;
            }
        }

        return (bodyY ?? 0f, legY ?? 0f, MathF.Abs((bodyY ?? 0f) - (legY ?? 0f)));
    }

    private static (float BodyMaxY, float LegMinY, float HullGap) MeasureBodyLegHullGap(
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

        var hullGap = legMinY - bodyMaxY;
        return (bodyMaxY, legMinY, hullGap);
    }
}
