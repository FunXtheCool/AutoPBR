using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Phase 5B — viewport AABB sanity after <c>ApplyLivingEntityRendererPreviewBasis</c> on assembly-parity pilots.
/// T1: <c>geometry_ir_assembly_viewport_strict_jvm.txt</c>. T2: quadruped-class probes on
/// <c>geometry-assembly-parity-pilots-26.1.2.txt</c> (skip when jar/shard missing).
/// </summary>
public sealed class GeometryIrAssemblyViewportSanityTests
{
    private const string VersionLabel = GeometryIrTestTierSupport.MobFamilyPilotVersionLabel;

    private static readonly MinecraftNativeProfile Profile26 =
        new(VersionLabel, "unused", new Version(26, 1, 2));

    private static readonly HashSet<string> StrictJvmSet =
        GeometryIrTestTierSupport.LoadAssemblyViewportStrictSet(GeometryIrTestTierSupport.FindRepoRoot());

    private static readonly IReadOnlyList<string> AssemblyParityPilots =
        GeometryAssemblyParityPilots.Load(GeometryIrTestTierSupport.FindRepoRoot(), VersionLabel)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

    public static IEnumerable<object[]> StrictCanaryCases =>
        StrictJvmSet.Select(jvm => new object[] { jvm });

    public static IEnumerable<object[]> AssemblyParityPilotCases =>
        AssemblyParityPilots.Select(jvm => new object[] { jvm });

    [Theory]
    [MemberData(nameof(StrictCanaryCases))]
    public void T1_strict_canary_legs_below_head_in_ler_preview_space(string officialJvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!GeometryIrTestTierSupport.IsClientJarPresent(repo))
        {
            return;
        }

        AssertViewportLegsBelowHead(officialJvmName, repo, isStrict: true);
    }

    [Theory]
    [MemberData(nameof(AssemblyParityPilotCases))]
    public void T2_assembly_pilot_quadruped_viewport_probe_when_assets_present(string officialJvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!GeometryIrTestTierSupport.IsClientJarPresent(repo))
        {
            return;
        }

        if (!TryLoadOkShardFromRepo(repo, officialJvmName, out var shardRoot))
        {
            return;
        }

        if (!IsQuadrupedHeadLegPilot(shardRoot))
        {
            return;
        }

        if (!GeometryIrTestTierSupport.RunAssemblyViewportProbeAssertions())
        {
            AssertParityMeshBuilds(officialJvmName, repo);
            return;
        }

        AssertViewportLegsBelowHead(officialJvmName, repo, isStrict: false);
    }

    private static void AssertParityMeshBuilds(string officialJvmName, string repo)
    {
        if (!TryLoadOkShardFromRepo(repo, officialJvmName, out var shardRoot))
        {
            return;
        }

        var (atlasW, atlasH) = GeometryIrTestTierSupport.ResolveParityAtlasSize(officialJvmName, shardRoot);
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            Profile26,
            officialJvmName,
            atlasW,
            atlasH,
            out var failure,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(failure);
        Assert.NotEmpty(mesh!.Elements);
    }

    private static void AssertViewportLegsBelowHead(string officialJvmName, string repo, bool isStrict)
    {
        if (!TryLoadOkShardFromRepo(repo, officialJvmName, out var shardRoot))
        {
            if (isStrict)
            {
                Assert.Fail($"{officialJvmName}: missing ok geometry shard under docs/generated/geometry/{VersionLabel}/");
            }

            return;
        }

        var (atlasW, atlasH) = GeometryIrTestTierSupport.ResolveParityAtlasSize(officialJvmName, shardRoot);
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            Profile26,
            officialJvmName,
            atlasW,
            atlasH,
            out var failure,
            geometryRootOverride: repaired);
        if (mesh is null)
        {
            if (isStrict)
            {
                Assert.Fail($"{officialJvmName}: parity mesh emit failed: {failure}");
            }

            return;
        }

        Assert.Null(failure);
        var (headY, legY, headParts, legParts) = MeasureHeadLegCentroidY(mesh, repaired, atlasW, atlasH, officialJvmName);
        Assert.True(
            legY < headY,
            $"{officialJvmName}: expected mean leg centroid Y ({legY:F3}) below head ({headY:F3}); " +
            $"head parts=[{string.Join(", ", headParts)}] leg parts=[{string.Join(", ", legParts)}]");
    }

    private static (float HeadY, float LegY, IReadOnlyList<string> HeadParts, IReadOnlyList<string> LegParts)
        MeasureHeadLegCentroidY(
            MergedJavaBlockModel mesh,
            JsonElement geometryRoot,
            int atlasW,
            int atlasH,
            string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            PreviewDegenerateAxisThickness = 0f,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        Assert.Equal(partIds.Count, mesh.Elements.Count);

        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        var headParts = new List<string>();
        var legParts = new List<string>();

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (IsHeadPartId(partId))
            {
                headSum += cy;
                headCount++;
                headParts.Add(partId);
            }

            if (IsLegPartId(partId))
            {
                legSum += cy;
                legCount++;
                legParts.Add(partId);
            }
        }

        Assert.True(headCount > 0, "expected at least one head-class part cuboid");
        Assert.True(legCount > 0, "expected at least one leg-class part cuboid");
        return (headSum / headCount, legSum / legCount, headParts, legParts);
    }

    private static bool IsHeadPartId(string partId) =>
        (partId.Contains("head", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(partId, "beak", StringComparison.Ordinal)) &&
        !IsLegPartId(partId);

    private static bool IsLegPartId(string partId) =>
        partId.Contains("leg", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuadrupedHeadLegPilot(JsonElement geometryRoot)
    {
        var ids = CollectPartIds(geometryRoot);
        if (!ids.Contains("head"))
        {
            return false;
        }

        return ids.Count(id => id.Contains("leg", StringComparison.OrdinalIgnoreCase)) >= 2;
    }

    private static HashSet<string> CollectPartIds(JsonElement geometryRoot)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var root in roots.EnumerateArray())
        {
            CollectPartIdsRecursive(root, ids);
        }

        return ids;
    }

    private static void CollectPartIdsRecursive(JsonElement part, HashSet<string> ids)
    {
        if (part.TryGetProperty("id", out var idEl))
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                CollectPartIdsRecursive(child, ids);
            }
        }
    }

    private static bool TryLoadOkShardFromRepo(string repoRoot, string officialJvmName, out JsonElement root)
    {
        root = default;
        var shardPath = Path.Combine(repoRoot, "docs", "generated", "geometry", VersionLabel, $"{officialJvmName}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        root = doc.RootElement.Clone();
        return true;
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 wMin, out Vector3 wMax)
    {
        wMin = new Vector3(float.PositiveInfinity);
        wMax = new Vector3(float.NegativeInfinity);
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
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }
    }
}
