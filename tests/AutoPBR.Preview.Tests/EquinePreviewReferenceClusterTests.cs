using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

/// <summary>
/// Adult equines use <see cref="AbstractEquineModel"/> IR (not the mis-lifted <c>HorseModel</c> baby shard)
/// and the same column-root LER + <c>Er × T</c> policy as other catalog mobs.
/// Baby cases extend runtime-ir-preview-plan § Baby JVM family (IR walk for tail; <c>BabyDonkeyModel</c> leg reparent only).
/// </summary>
public sealed class EquinePreviewReferenceClusterTests
{
    [Theory]
    [InlineData(
        "assets/minecraft/textures/entity/horse/horse_white.png",
        "net.minecraft.client.model.animal.equine.AbstractEquineModel",
        12)]
    [InlineData(
        "assets/minecraft/textures/entity/horse/donkey.png",
        "net.minecraft.client.model.animal.equine.DonkeyModel",
        12)]
    [InlineData(
        "assets/minecraft/textures/entity/horse/horse_white_baby.png",
        "net.minecraft.client.model.animal.equine.BabyHorseModel",
        10)]
    [InlineData(
        "assets/minecraft/textures/entity/horse/donkey_baby.png",
        "net.minecraft.client.model.animal.equine.BabyDonkeyModel",
        10)]
    [InlineData(
        "assets/minecraft/textures/entity/horse/mule_baby.png",
        "net.minecraft.client.model.animal.equine.BabyDonkeyModel",
        10)]
    public void Catalog_static_mesh_resolves_adult_equine_shard_and_clusters_body_with_head(
        string texturePath,
        string expectedJvm,
        int minElements)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, idlePhase01: 0.2f, animationTimeSeconds: 0f, out var mesh));
        Assert.True(mesh.Elements.Count >= minElements, $"elements={mesh.Elements.Count}");

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{expectedJvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(expectedJvm, shard.RootElement);
        var options = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = expectedJvm };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);

        float bodyY = 0f, headY = 0f;
        var bodyN = 0;
        var headN = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var cy = CornerCentroidY(mesh.Elements[i]);
            if (string.Equals(partIds[i], "body", StringComparison.Ordinal))
            {
                bodyY += cy;
                bodyN++;
            }
            else if (IsEquineHeadClusterPart(partIds[i]))
            {
                headY += cy;
                headN++;
            }
        }

        Assert.True(bodyN > 0 && headN > 0, $"{expectedJvm}: body={bodyN} head={headN}");
        bodyY /= bodyN;
        headY /= headN;
        Assert.True(headY > bodyY - 6f, $"{expectedJvm}: headY={headY:F3} bodyY={bodyY:F3}");
        Assert.True(bodyY > headY - 20f, $"{expectedJvm}: body should not be far above head; headY={headY:F3} bodyY={bodyY:F3}");
    }

    [Fact]
    public void Adult_horse_jvm_resolver_prefers_AbstractEquineModel_over_HorseModel()
    {
        var rule = EntityTextureParityCatalog.ResolveRule(
            "assets/minecraft/textures/entity/horse/horse_white.png",
            "horse_white");
        Assert.NotNull(rule);
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            profile,
            rule!,
            "assets/minecraft/textures/entity/horse/horse_white.png",
            "horse_white",
            isBaby: false,
            out var jvm,
            out _));
        Assert.Equal("net.minecraft.client.model.animal.equine.AbstractEquineModel", jvm);
    }

    [Theory]
    [InlineData(
        "assets/minecraft/textures/entity/horse/horse_white_baby.png",
        "horse_white_baby",
        "net.minecraft.client.model.animal.equine.BabyHorseModel")]
    [InlineData(
        "assets/minecraft/textures/entity/horse/donkey_baby.png",
        "donkey_baby",
        "net.minecraft.client.model.animal.equine.BabyDonkeyModel")]
    [InlineData(
        "assets/minecraft/textures/entity/horse/mule_baby.png",
        "mule_baby",
        "net.minecraft.client.model.animal.equine.BabyDonkeyModel")]
    public void Baby_equine_jvm_resolver_prefers_baby_shard_over_adult_hosts(
        string texturePath,
        string stem,
        string expectedJvm)
    {
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            profile,
            rule!,
            texturePath,
            stem,
            isBaby: true,
            out var jvm,
            out _));
        Assert.Equal(expectedJvm, jvm);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/horse/donkey_baby.png")]
    [InlineData("assets/minecraft/textures/entity/horse/mule_baby.png")]
    public void Baby_donkey_catalog_mesh_legs_cluster_with_body(string texturePath)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, idlePhase01: 0.2f, animationTimeSeconds: 0f, out var mesh));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            profile, rule!, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm });

        float bodyY = 0f;
        var bodyN = 0;
        var legYs = new List<float>();
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var cy = CornerCentroidY(mesh.Elements[i]);
            var partId = partIds[i];
            if (string.Equals(partId, "body", StringComparison.Ordinal))
            {
                bodyY += cy;
                bodyN++;
            }
            else if (partId.Contains("leg", StringComparison.Ordinal))
            {
                legYs.Add(cy);
            }
        }

        Assert.True(bodyN > 0 && legYs.Count >= 4, $"{texturePath}: body={bodyN} legs={legYs.Count}");
        bodyY /= bodyN;
        foreach (var legY in legYs)
        {
            Assert.True(MathF.Abs(legY - bodyY) < 12f, $"{texturePath}: legY={legY:F3} bodyY={bodyY:F3}");
        }
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/horse/horse_black_baby.png")]
    [InlineData("assets/minecraft/textures/entity/horse/donkey_baby.png")]
    public void Baby_equine_catalog_mesh_tail_clusters_with_body(string texturePath)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, idlePhase01: 0.2f, animationTimeSeconds: 0f, out var mesh));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            profile, rule!, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm });

        float bodyY = 0f, bodyZ = 0f;
        var bodyN = 0;
        var tailYs = new List<float>();
        var tailZs = new List<float>();
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var cy = CornerCentroidY(mesh.Elements[i]);
            var cz = CornerCentroidZ(mesh.Elements[i]);
            if (string.Equals(partId, "body", StringComparison.Ordinal))
            {
                bodyY += cy;
                bodyZ += cz;
                bodyN++;
            }
            else if (partId.Contains("tail", StringComparison.Ordinal))
            {
                tailYs.Add(cy);
                tailZs.Add(cz);
            }
        }

        Assert.True(bodyN > 0 && tailYs.Count > 0, $"{texturePath}: body={bodyN} tail={tailYs.Count}");
        bodyY /= bodyN;
        bodyZ /= bodyN;
        foreach (var tailY in tailYs)
        {
            Assert.True(MathF.Abs(tailY - bodyY) < 8f, $"{texturePath}: tailY={tailY:F3} bodyY={bodyY:F3}");
        }

        foreach (var tailZ in tailZs)
        {
            Assert.True(tailZ >= bodyZ - 4f, $"{texturePath}: tailZ={tailZ:F3} bodyZ={bodyZ:F3}");
            Assert.True(tailZ - bodyZ < 16f, $"{texturePath}: tail too far behind body tailZ={tailZ:F3} bodyZ={bodyZ:F3}");
        }
    }

    private static float CornerCentroidZ(ModelElement el)
    {
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
        var wMin = new Vector3(float.PositiveInfinity);
        var wMax = new Vector3(float.NegativeInfinity);
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }

        return (wMin.Z + wMax.Z) * 0.5f;
    }

    private static bool IsEquineHeadClusterPart(string partId) =>
        string.Equals(partId, "head_parts", StringComparison.Ordinal) ||
        string.Equals(partId, "head_r1", StringComparison.Ordinal) ||
        string.Equals(partId, "neck_r1", StringComparison.Ordinal);

    private static float CornerCentroidY(ModelElement el)
    {
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
        var wMin = new Vector3(float.PositiveInfinity);
        var wMax = new Vector3(float.NegativeInfinity);
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }

        return (wMin.Y + wMax.Y) * 0.5f;
    }
}
