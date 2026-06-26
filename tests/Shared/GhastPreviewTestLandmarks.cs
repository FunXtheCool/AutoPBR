using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Tests.TestSupport;

/// <summary>
/// Shared ghast-family IR landmark gates derived from the pinned Java reference bake.
/// </summary>
internal static class GhastPreviewTestLandmarks
{
    public const string MonsterJvm = "net.minecraft.client.model.monster.ghast.GhastModel";
    public const string HappyJvm = "net.minecraft.client.model.animal.ghast.HappyGhastModel";
    public const string MonsterTexturePath = "assets/minecraft/textures/entity/ghast/ghast.png";
    public const string HappyTexturePath = "assets/minecraft/textures/entity/ghast/happy_ghast.png";

    public static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    /// <summary>Committed T1 shard must exist with <c>extractionStatus: ok</c> — do not silently skip.</summary>
    public static string RequireOkCommittedShardPath(string officialJvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(
            repo,
            "docs",
            "generated",
            "geometry",
            "26.1.2",
            $"{officialJvmName}.json");
        Assert.True(File.Exists(shardPath), $"Committed geometry IR shard missing: {shardPath}");
        Assert.True(
            GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status),
            $"Could not read extractionStatus from {shardPath}");
        Assert.Equal("ok", status);
        return shardPath;
    }

    public static void AssertRuntimeGeometryIrDriver(PreviewMeshProvenance provenance, string expectedJvmFragment)
    {
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(expectedJvmFragment, provenance.Detail ?? "", StringComparison.Ordinal);
    }

    public static void AssertNotCleanRoomBodyLocalCube(MergedJavaBlockModel bind)
    {
        Assert.True(bind.Elements.Count >= 10, "expected body + 9 tentacles");
        var body = bind.Elements[0];
        var bodyLocalMinY = MathF.Min(body.From[1], body.To[1]);
        var bodyLocalMaxY = MathF.Max(body.From[1], body.To[1]);
        // CleanRoom BuildGhast body local Y is [9.6, 25.6] at identity root; IR body is centered 16³.
        Assert.InRange(bodyLocalMinY, -8.1f, 8.1f);
        Assert.InRange(bodyLocalMaxY, -8.1f, 8.1f);
        Assert.True(bodyLocalMaxY - bodyLocalMinY >= 15.5f, "IR ghast body should span ~16 texels in Y");
    }

    public static void AssertGhastRuntimeMatchesReferenceAffines(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds,
        string officialJvmName,
        float animationTimeSeconds,
        float tolerance = 0.0015f)
    {
        Assert.True(mesh.UsesLivingEntityRendererColumnYFlip);
        Assert.Equal(mesh.Elements.Count, partIds.Count);

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(
            repo,
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            $"{officialJvmName}.json");
        Assert.True(File.Exists(referencePath), $"Java reference bake missing: {referencePath}");

        using var reference = System.Text.Json.JsonDocument.Parse(File.ReadAllText(referencePath));
        var affines = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var affine in reference.RootElement.GetProperty("renderPartAffines").EnumerateArray())
        {
            var id = affine.GetProperty("id").GetString();
            if (!string.IsNullOrEmpty(id))
            {
                affines[id] = ReadReferenceRenderAffineTexel(affine.GetProperty("matrixRowMajor"));
            }
        }

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            Assert.True(affines.TryGetValue(partId, out var expected), $"reference affine missing for {partId}");
            if (GeometryIrEmitPolicy.TryParseGhastFamilyTentacleIndex(partId, out var tentacleIndex))
            {
                var pitch = GeometryIrEmitPolicy.ComputeGhastAnimateTentaclesXRot(
                    tentacleIndex,
                    animationTimeSeconds);
                expected = Matrix4x4.Multiply(Matrix4x4.CreateRotationX(pitch), expected);
            }

            expected = CleanRoomEntityModelRuntime.ApplyLivingEntityRendererColumnRootScale(expected);
            AssertMatrixClose(expected, mesh.Elements[i].LocalToParent, partId, tolerance);
        }
    }

    public static int FindBodyElementIndex(IReadOnlyList<string> partIds)
    {
        for (var i = 0; i < partIds.Count; i++)
        {
            if (string.Equals(partIds[i], "body", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static Matrix4x4 ReadReferenceRenderAffineTexel(System.Text.Json.JsonElement rows)
    {
        return new Matrix4x4(
            (float)rows[0][0].GetDouble(), (float)rows[0][1].GetDouble(),
            (float)rows[0][2].GetDouble(), (float)rows[0][3].GetDouble(),
            (float)rows[1][0].GetDouble(), (float)rows[1][1].GetDouble(),
            (float)rows[1][2].GetDouble(), (float)rows[1][3].GetDouble(),
            (float)rows[2][0].GetDouble(), (float)rows[2][1].GetDouble(),
            (float)rows[2][2].GetDouble(), (float)rows[2][3].GetDouble(),
            (float)rows[3][0].GetDouble() * 16f, (float)rows[3][1].GetDouble() * 16f,
            (float)rows[3][2].GetDouble() * 16f, (float)rows[3][3].GetDouble());
    }

    private static void AssertMatrixClose(Matrix4x4 expected, Matrix4x4 actual, string partId, float tolerance)
    {
        ReadOnlySpan<float> expectedValues =
        [
            expected.M11, expected.M12, expected.M13, expected.M14,
            expected.M21, expected.M22, expected.M23, expected.M24,
            expected.M31, expected.M32, expected.M33, expected.M34,
            expected.M41, expected.M42, expected.M43, expected.M44,
        ];
        ReadOnlySpan<float> actualValues =
        [
            actual.M11, actual.M12, actual.M13, actual.M14,
            actual.M21, actual.M22, actual.M23, actual.M24,
            actual.M31, actual.M32, actual.M33, actual.M34,
            actual.M41, actual.M42, actual.M43, actual.M44,
        ];
        for (var i = 0; i < expectedValues.Length; i++)
        {
            Assert.True(
                MathF.Abs(expectedValues[i] - actualValues[i]) <= tolerance,
                $"{partId} matrix[{i}] expected={expectedValues[i]:F6} actual={actualValues[i]:F6}");
        }
    }
}
