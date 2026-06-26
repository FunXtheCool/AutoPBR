using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BeeWingComposeProbeTests
{
    private const string BeeJvm = "net.minecraft.client.model.animal.bee.AdultBeeModel";
    private const string TexturePath = "assets/minecraft/textures/entity/bee/bee.png";

    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    private static readonly Vector3 RightWingJvmCenter = new(-6.623124123f, 15f, -1.266911268f);
    private static readonly Vector3 LeftWingJvmCenter = new(6.623124123f, 15f, -1.266911268f);

    [Fact]
    public void Adult_bee_wing_cuboid_center_matches_jvm_render_center_after_ler()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath, Profile26, 0f, 0f, out var mesh, out _, applyGeometryIrSetupAnimMotion: false), TexturePath);

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{BeeJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(BeeJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = BeeJvm });

        var rightCenter = MeasureElementCuboidCenter(mesh, partIds, "right_wing");
        var leftCenter = MeasureElementCuboidCenter(mesh, partIds, "left_wing");
        var expectedRight = Vector3.Transform(RightWingJvmCenter, CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale);
        var expectedLeft = Vector3.Transform(LeftWingJvmCenter, CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale);

        Assert.True(Vector3.Distance(rightCenter, expectedRight) <= 0.12f,
            $"right_wing center mesh={rightCenter} ler(jvm)={expectedRight} dist={Vector3.Distance(rightCenter, expectedRight):F4}");
        Assert.True(Vector3.Distance(leftCenter, expectedLeft) <= 0.12f,
            $"left_wing center mesh={leftCenter} ler(jvm)={expectedLeft} dist={Vector3.Distance(leftCenter, expectedLeft):F4}");
    }

    [Fact]
    public void Adult_bee_cpu_rebake_wing_sheet_stays_near_body_shell()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            TexturePath, Profile26, 0f, 0f, out var merged, out _, applyGeometryIrSetupAnimMotion: false), TexturePath);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [ordered[0]] = (64, 64) };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var packVerts, out _, out _));

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = TexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = [TexturePath],
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
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{BeeJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            GeometryIrPartTreeRepair.ApplyForParityCatalog(BeeJvm, shard.RootElement),
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = BeeJvm });

        var bodyBounds = MeasurePreviewBounds(rebakedVerts!, stride, merged, partIds, "body");
        var rightBounds = MeasurePreviewBounds(rebakedVerts!, stride, merged, partIds, "right_wing");
        var leftBounds = MeasurePreviewBounds(rebakedVerts!, stride, merged, partIds, "left_wing");

        var rightGap = SeparatedDistance(bodyBounds, rightBounds);
        var leftGap = SeparatedDistance(bodyBounds, leftBounds);
        Assert.True(rightGap <= 0.08f, $"right wing rebake gap from body={rightGap:F4} body={bodyBounds} wing={rightBounds}");
        Assert.True(leftGap <= 0.08f, $"left wing rebake gap from body={leftGap:F4} body={bodyBounds} wing={leftBounds}");
    }

    [Fact]
    public void Adult_bee_wing_block_stack_matches_jvm_render_center_after_ler()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{BeeJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(BeeJvm, shard.RootElement);

        var blockMesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            TexturePath,
            Profile26,
            BeeJvm,
            64,
            64,
            out var err,
            geometryRootOverride: repaired);
        Assert.Null(err);
        Assert.NotNull(blockMesh);

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = BeeJvm });
        var blockCenter = MeasureElementCuboidCenter(blockMesh, partIds, "right_wing");
        var expectedRight = Vector3.Transform(RightWingJvmCenter, CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale);

        Assert.True(Vector3.Distance(blockCenter, expectedRight) <= 0.12f,
            $"block-stack right_wing center={blockCenter} ler(jvm)={expectedRight} dist={Vector3.Distance(blockCenter, expectedRight):F4}");
    }

    private static Vector3 MeasureElementCuboidCenter(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        string partId)
    {
        for (var i = 0; i < mesh.Elements.Count && i < partIds.Count; i++)
        {
            if (!string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                continue;
            }

            var el = mesh.Elements[i];
            var localCenter = new Vector3(
                (el.From[0] + el.To[0]) * 0.5f,
                (el.From[1] + el.To[1]) * 0.5f,
                (el.From[2] + el.To[2]) * 0.5f);
            return Vector3.Transform(localCenter, el.LocalToParent);
        }

        throw new InvalidOperationException($"part '{partId}' not found in mesh");
    }

    private readonly record struct PreviewBounds(Vector3 Min, Vector3 Max)
    {
        public static PreviewBounds FromPoint(Vector3 p) => new(p, p);

        public PreviewBounds Include(Vector3 p) => new(Vector3.Min(Min, p), Vector3.Max(Max, p));

        public PreviewBounds Union(PreviewBounds other) =>
            new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

        public override string ToString() => $"[{Min}, {Max}]";
    }

    private static PreviewBounds MeasurePreviewBounds(
        ReadOnlySpan<float> vertices,
        int stride,
        MergedJavaBlockModel model,
        List<string> partIds,
        string partId)
    {
        PreviewBounds? bounds = null;
        var floatOffset = 0;
        for (var ei = 0; ei < model.Elements.Count && ei < partIds.Count; ei++)
        {
            if (!string.Equals(partIds[ei], partId, StringComparison.Ordinal))
            {
                floatOffset += model.Elements[ei].Faces.Count * 4 * stride;
                continue;
            }

            var vertexCount = model.Elements[ei].Faces.Count * 4;
            for (var vi = 0; vi < vertexCount && floatOffset + stride - 1 < vertices.Length; vi++, floatOffset += stride)
            {
                var p = new Vector3(vertices[floatOffset], vertices[floatOffset + 1], vertices[floatOffset + 2]);
                bounds = bounds is { } existing ? existing.Include(p) : PreviewBounds.FromPoint(p);
            }

            break;
        }

        return bounds ?? throw new InvalidOperationException($"part '{partId}' not found in rebaked vertices");
    }

    private static float SeparatedDistance(PreviewBounds a, PreviewBounds b)
    {
        var dx = MathF.Max(0f, MathF.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
        var dy = MathF.Max(0f, MathF.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
        var dz = MathF.Max(0f, MathF.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
