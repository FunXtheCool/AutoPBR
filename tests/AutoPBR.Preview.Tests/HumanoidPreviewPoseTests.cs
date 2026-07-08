using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

public sealed class HumanoidPreviewPoseTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string PlayerWidePath = "assets/minecraft/textures/entity/player/wide/steve.png";
    private const string ZombieVillagerPath =
        "assets/minecraft/textures/entity/zombie_villager/profession/leatherworker.png";

    [Fact]
    public void TryGetPoseOptions_returns_humanoid_poses_for_player_wide()
    {
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(PlayerWidePath, "PlayerWide", out var options));
        Assert.Equal(7, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.HumanoidEmpty);
    }

    [Fact]
    public void TryGetPoseOptions_includes_zombie_arms_default_for_zombie_villager()
    {
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(
            ZombieVillagerPath,
            "HumanoidZombieVillager",
            out var options));
        Assert.Equal(8, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.HumanoidZombieArms);
    }

    [Theory]
    [InlineData("PlayerWide", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("PlayerSlim", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("HumanoidGeneric", EntityHumanoidPreviewArmPose.Empty)]
    [InlineData("HumanoidZombieVillager", EntityHumanoidPreviewArmPose.ZombieArms)]
    public void ResolveEffectiveHumanoidArmPose_uses_builder_default_when_no_selector(
        string builderMethod,
        EntityHumanoidPreviewArmPose expected)
    {
        var pose = EntityPreviewPoseCatalog.ResolveEffectiveHumanoidArmPose(builderMethod, selectedPoseId: null);
        Assert.Equal(expected, pose);
    }

    [Fact]
    public void Player_bow_and_empty_selector_poses_produce_different_static_meshes()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel empty;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PlayerWidePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out empty,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotEmpty(empty.Elements);
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidBowAndArrow))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PlayerWidePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var bow,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(empty.Elements.Count, bow.Elements.Count);
            var maxErr = MaxMatrixDelta(empty.Elements[3].LocalToParent, bow.Elements[3].LocalToParent);
            Assert.True(maxErr > 0.05f, $"expected arm pose delta, got {maxErr:F4}");
        }
    }

    [Fact]
    public void Zombie_villager_zombie_arms_and_empty_poses_produce_different_static_meshes()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel zombieArms;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                ZombieVillagerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out zombieArms,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotEmpty(zombieArms.Elements);
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                ZombieVillagerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var empty,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            var maxErr = 0f;
            for (var i = 0; i < zombieArms.Elements.Count; i++)
            {
                maxErr = MathF.Max(
                    maxErr,
                    MaxMatrixDelta(zombieArms.Elements[i].LocalToParent, empty.Elements[i].LocalToParent));
            }

            Assert.True(maxErr > 0.05f, $"expected arm pose delta, got {maxErr:F4}");
        }
    }

    [Fact]
    public void Baby_zombie_villager_rebake_placement_diagnostics_stay_compact()
    {
        const string path = "assets/minecraft/textures/entity/zombie_villager/baby/desert.png";
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel merged;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                path,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out merged,
                out _,
                applyGeometryIrSetupAnimMotion: true));
        }

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = path,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = Profile26.Name,
            NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            PreviewPoseId = EntityPreviewPoseCatalog.HumanoidZombieArms,
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = ordered.ToArray()
        };

        var materials = ordered.Select(_ => CreatePreviewMaps(64, 64)).ToArray();
        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            materials,
            animationTimeSeconds: 0f,
            out _,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: true));

        Assert.InRange(MathF.Abs(rebake.LastHeadCentroidY - rebake.LastBodyCentroidY), 0f, 0.75f);
        Assert.InRange(MathF.Abs(rebake.LastBodyCentroidY - rebake.LastLegCentroidY), 0f, 0.75f);
    }

    [Fact]
    public void Baby_zombie_villager_base_texture_arms_stay_attached_with_zombie_arms_pose()
    {
        const string path = "assets/minecraft/textures/entity/zombie_villager/zombie_villager_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel mesh;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                path,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out mesh,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotEmpty(mesh.Elements);
        }

        var stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, path, stem, isBaby: true, out var jvm, out var geometryRoot));
        Assert.Equal("net.minecraft.client.model.monster.zombie.BabyZombieVillagerModel", jvm);
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });

        var bodyCentroid = ComputePartPreviewCentroid(mesh, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        var armCentroid = ComputePartPreviewCentroid(mesh, partIds, static id =>
            id.Contains("arm", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue && armCentroid.HasValue);
        var xGap = MathF.Abs(bodyCentroid.Value.X - armCentroid.Value.X);
        var zForward = armCentroid.Value.Z - bodyCentroid.Value.Z;
        Assert.True(
            xGap <= 0.35f,
            $"arm-body lateral gap={xGap:F3} bodyX={bodyCentroid.Value.X:F3} armX={armCentroid.Value.X:F3}");
        Assert.True(
            zForward >= -0.15f,
            $"arms should extend forward of torso, zForward={zForward:F3} bodyZ={bodyCentroid.Value.Z:F3} armZ={armCentroid.Value.Z:F3}");
    }

    private static Vector3 MeasureElementPreviewCentroid(ModelElement el)
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
            var model = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            var preview = new Vector3(model.X / 16f - 0.5f, model.Y / 16f - 0.5f, model.Z / 16f - 0.5f);
            wMin = Vector3.Min(wMin, preview);
            wMax = Vector3.Max(wMax, preview);
        }

        return (wMin + wMax) * 0.5f;
    }

    private static Vector3? ComputePartPreviewCentroid(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        Func<string, bool> partFilter)
    {
        Vector3 sum = Vector3.Zero;
        var count = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (!partFilter(partIds[i]))
            {
                continue;
            }

            var c = MeasureElementPreviewCentroid(mesh.Elements[i]);
            sum += c;
            count++;
        }

        return count == 0 ? null : sum / count;
    }

    private static PreviewTextureMaps CreatePreviewMaps(int width, int height) => new()
    {
        Width = width,
        Height = height,
        DiffuseRgba = new byte[width * height * 4],
        NormalRgba = new byte[width * height * 4],
        SpecularRgba = new byte[width * height * 4],
        HeightRgba = new byte[width * height * 4],
    };

    private static float MaxMatrixDelta(System.Numerics.Matrix4x4 a, System.Numerics.Matrix4x4 b)
    {
        var max = 0f;
        max = MathF.Max(max, MathF.Abs(a.M11 - b.M11));
        max = MathF.Max(max, MathF.Abs(a.M12 - b.M12));
        max = MathF.Max(max, MathF.Abs(a.M13 - b.M13));
        max = MathF.Max(max, MathF.Abs(a.M14 - b.M14));
        max = MathF.Max(max, MathF.Abs(a.M21 - b.M21));
        max = MathF.Max(max, MathF.Abs(a.M22 - b.M22));
        max = MathF.Max(max, MathF.Abs(a.M23 - b.M23));
        max = MathF.Max(max, MathF.Abs(a.M24 - b.M24));
        max = MathF.Max(max, MathF.Abs(a.M31 - b.M31));
        max = MathF.Max(max, MathF.Abs(a.M32 - b.M32));
        max = MathF.Max(max, MathF.Abs(a.M33 - b.M33));
        max = MathF.Max(max, MathF.Abs(a.M34 - b.M34));
        max = MathF.Max(max, MathF.Abs(a.M41 - b.M41));
        max = MathF.Max(max, MathF.Abs(a.M42 - b.M42));
        max = MathF.Max(max, MathF.Abs(a.M43 - b.M43));
        max = MathF.Max(max, MathF.Abs(a.M44 - b.M44));
        return max;
    }
}
