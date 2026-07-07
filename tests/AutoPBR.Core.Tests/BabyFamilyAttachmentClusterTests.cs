using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Baby JVM meshes must keep limbs/tail within plausible joint distance of the torso (preview space after LER).
/// Guardrail tests for runtime-ir-preview-plan § Baby JVM family (extends adult quadruped world-preview policy).
/// </summary>
public sealed class BabyFamilyAttachmentClusterTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    public static TheoryData<string, float> BabyAttachmentCases => new()
    {
        { "assets/minecraft/textures/entity/armadillo/armadillo_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/fox/fox_baby.png", 0.55f },
        { "assets/minecraft/textures/entity/cow/cow_temperate_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png", 0.55f },
        { "assets/minecraft/textures/entity/cat/cat_british_shorthair_baby.png", 0.55f },
        { "assets/minecraft/textures/entity/bear/polarbear_baby.png", 0.55f },
        { "assets/minecraft/textures/entity/horse/horse_black_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/horse/donkey_baby.png", 0.95f },
        { "assets/minecraft/textures/entity/goat/goat_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/sheep/sheep_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/zombie/drowned_baby.png", 0.65f },
        { "assets/minecraft/textures/entity/zombie_villager/baby/desert.png", 0.75f },
        { "assets/minecraft/textures/entity/zombie_villager/zombie_villager_baby.png", 0.75f },
        { "assets/minecraft/textures/entity/sniffer/snifflet.png", 0.95f },
    };

    [Fact]
    public void Baby_zombie_villager_gpu_anim_motion_head_stays_near_body()
    {
        const string texturePath = "assets/minecraft/textures/entity/zombie_villager/baby/desert.png";
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        MergedJavaBlockModel merged;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                texturePath, Profile26, 0f, 0.5f, out merged, applyGeometryIrSetupAnimMotion: true));
        }

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = texturePath,
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
            animationTimeSeconds: 0.5f,
            out var verts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: true));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });

        var bodyCentroid = ComputeBakedPartPreviewCentroid(merged, verts!, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        var headCentroid = ComputeBakedPartPreviewCentroid(merged, verts!, partIds, static id =>
            id.Contains("head", StringComparison.Ordinal) && !id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue && headCentroid.HasValue);
        Assert.True(
            Vector3.Distance(bodyCentroid.Value, headCentroid.Value) <= 0.75f,
            $"head-body gap={Vector3.Distance(bodyCentroid.Value, headCentroid.Value):F3}");
    }

    [Fact]
    public void Baby_zombie_villager_rebaked_cpu_vertices_stay_clustered_with_zombie_arms_pose()
    {
        const string texturePath = "assets/minecraft/textures/entity/zombie_villager/baby/desert.png";
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        MergedJavaBlockModel merged;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                texturePath, Profile26, 0f, 0f, out merged, applyGeometryIrSetupAnimMotion: true));
        }

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = texturePath,
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
            out var verts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: true));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });

        var bodyCentroid = ComputeBakedPartPreviewCentroid(merged, verts!, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        var headCentroid = ComputeBakedPartPreviewCentroid(merged, verts!, partIds, static id =>
            id.Contains("head", StringComparison.Ordinal) && !id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue && headCentroid.HasValue);
        Assert.True(Vector3.Distance(bodyCentroid.Value, headCentroid.Value) <= 0.75f);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/zombie_villager/baby/desert.png")]
    [InlineData("assets/minecraft/textures/entity/zombie_villager/zombie_villager_baby.png")]
    public void Baby_zombie_villager_zombie_arms_pose_keeps_arms_laterally_attached(string texturePath)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        MergedJavaBlockModel mesh;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                texturePath, Profile26, 0f, 0f, out mesh, applyGeometryIrSetupAnimMotion: false));
        }

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
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
        Assert.True(
            xGap <= 0.35f,
            $"{texturePath} zombie-arms lateral gap {xGap:F3} bodyX={bodyCentroid.Value.X:F3} armX={armCentroid.Value.X:F3}");
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

    [Theory]
    [MemberData(nameof(BabyAttachmentCases))]
    public void Catalog_baby_limb_and_tail_centroids_stay_near_body(string texturePath, float maxGapPreviewUnits)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });
        Assert.Equal(mesh.Elements.Count, partIds.Count);

        var bodyCentroid = ComputePartPreviewCentroid(mesh, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue, $"{texturePath}: missing body cluster");

        foreach (var suffix in new[] { "leg", "tail", "head" })
        {
            var partCentroid = ComputePartPreviewCentroid(mesh, partIds, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            if (!partCentroid.HasValue)
            {
                continue;
            }

            var maxGap = suffix == "head" ? MathF.Max(0.75f, maxGapPreviewUnits) : maxGapPreviewUnits;
            var gap = Vector3.Distance(bodyCentroid.Value, partCentroid.Value);
            Assert.True(
                gap <= maxGap,
                $"{texturePath} {suffix}: body-part gap {gap:F3} > {maxGap:F3} " +
                $"body=({bodyCentroid.Value.X:F3},{bodyCentroid.Value.Y:F3},{bodyCentroid.Value.Z:F3}) " +
                $"part=({partCentroid.Value.X:F3},{partCentroid.Value.Y:F3},{partCentroid.Value.Z:F3})");
        }
    }

    [Theory]
    [MemberData(nameof(BabyAttachmentCases))]
    public void Baked_cpu_vertices_cluster_like_local_to_parent(string texturePath, float maxGapPreviewUnits)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });
        Assert.Equal(merged.Elements.Count, partIds.Count);

        var bodyCentroid = ComputeBakedPartPreviewCentroid(merged, verts, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue, $"{texturePath}: missing baked body cluster");

        foreach (var suffix in new[] { "leg", "tail", "head" })
        {
            var partCentroid = ComputeBakedPartPreviewCentroid(merged, verts, partIds, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            if (!partCentroid.HasValue)
            {
                continue;
            }

            var maxGap = suffix == "head" ? MathF.Max(0.75f, maxGapPreviewUnits) : maxGapPreviewUnits;
            var gap = Vector3.Distance(bodyCentroid.Value, partCentroid.Value);
            Assert.True(
                gap <= maxGap,
                $"{texturePath} baked {suffix}: body-part gap {gap:F3} > {maxGap:F3}");
        }
    }

    [Theory]
    [MemberData(nameof(BabyAttachmentCases))]
    public void Gpu_bind_pose_shader_path_keeps_baby_parts_clustered(string texturePath, float maxGapPreviewUnits)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mergedBind));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedBind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mergedBind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });
        Assert.Equal(mergedBind.Elements.Count, partIds.Count);

        EntityPreviewPlacement.ApplyToGpuBindVertices(gpuVerts, partIds);

        var bodyCentroid = ComputeGpuPlacedPartPreviewCentroid(mergedBind, gpuVerts, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue, $"{texturePath}: missing GPU body cluster");

        foreach (var suffix in new[] { "leg", "tail", "head" })
        {
            var partCentroid = ComputeGpuPlacedPartPreviewCentroid(mergedBind, gpuVerts, partIds, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            if (!partCentroid.HasValue)
            {
                continue;
            }

            var maxGap = suffix == "head" ? MathF.Max(0.75f, maxGapPreviewUnits) : maxGapPreviewUnits;
            var gap = Vector3.Distance(bodyCentroid.Value, partCentroid.Value);
            Assert.True(
                gap <= maxGap,
                $"{texturePath} GPU {suffix}: body-part gap {gap:F3} > {maxGap:F3}");
        }
    }

    private static Vector3? ComputeGpuPlacedPartPreviewCentroid(
        MergedJavaBlockModel mesh,
        float[] verts,
        List<string> partIds,
        Func<string, bool> match)
    {
        var sum = Vector3.Zero;
        var count = 0;
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var vertexBase = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var vertCount = CountEmittedFaces(mesh.Elements[e]) * 4;
            if (match(partIds[e]))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vertexBase + v) * stride;
                    var texel = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                    sum += EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(texel);
                    count++;
                }
            }

            vertexBase += vertCount;
        }

        return count > 0 ? sum / count : null;
    }

    [Theory]
    [MemberData(nameof(BabyAttachmentCases))]
    public void Explore_placement_pipeline_keeps_baby_parts_clustered(string texturePath, float maxGapPreviewUnits)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var merged));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 64);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });
        Assert.Equal(merged.Elements.Count, partIds.Count);

        EntityPreviewPlacement.ApplyToPreviewVertices(
            verts,
            MinecraftModelBaker.FloatsPerVertex,
            partIds);

        var bodyCentroid = ComputeBakedPartPreviewCentroid(merged, verts, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue, $"{texturePath}: missing placed body cluster");

        foreach (var suffix in new[] { "leg", "tail", "head" })
        {
            var partCentroid = ComputeBakedPartPreviewCentroid(merged, verts, partIds, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            if (!partCentroid.HasValue)
            {
                continue;
            }

            var maxGap = suffix == "head" ? MathF.Max(0.75f, maxGapPreviewUnits) : maxGapPreviewUnits;
            var gap = Vector3.Distance(bodyCentroid.Value, partCentroid.Value);
            Assert.True(
                gap <= maxGap,
                $"{texturePath} placed {suffix}: body-part gap {gap:F3} > {maxGap:F3}");
        }
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/fox/fox_baby.png")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_british_shorthair_baby.png")]
    public void Setup_anim_keeps_baby_family_parts_clustered(string texturePath)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(
            texturePath, Profile26, 0.2f, animationTimeSeconds: 1.25f, out var animated,
            applyGeometryIrSetupAnimMotion: true));

        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out var geometryRoot));
        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });

        var bodyCentroid = ComputePartPreviewCentroid(animated, partIds, static id =>
            id.Contains("body", StringComparison.Ordinal));
        Assert.True(bodyCentroid.HasValue, $"{texturePath}: missing body cluster");

        foreach (var suffix in new[] { "leg", "tail", "head", "wing" })
        {
            var partCentroid = ComputePartPreviewCentroid(animated, partIds, id =>
                id.Contains(suffix, StringComparison.Ordinal) &&
                !id.Contains("body", StringComparison.Ordinal));
            if (!partCentroid.HasValue)
            {
                continue;
            }

            var gap = Vector3.Distance(bodyCentroid.Value, partCentroid.Value);
            Assert.True(
                gap <= 0.65f,
                $"{texturePath} animated {suffix}: body-part gap {gap:F3}");
        }
    }

    private static Vector3? ComputePartPreviewCentroid(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        Func<string, bool> match)
    {
        var sum = Vector3.Zero;
        var count = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            if (!match(partIds[i]))
            {
                continue;
            }

            sum += MeasureElementPreviewCentroid(mesh.Elements[i]);
            count++;
        }

        return count > 0 ? sum / count : null;
    }

    private static Vector3? ComputeBakedPartPreviewCentroid(
        MergedJavaBlockModel mesh,
        float[] verts,
        List<string> partIds,
        Func<string, bool> match)
    {
        var sum = Vector3.Zero;
        var count = 0;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var vertexBase = 0;
        for (var e = 0; e < mesh.Elements.Count; e++)
        {
            var faceCount = CountEmittedFaces(mesh.Elements[e]);
            var vertCount = faceCount * 4;
            if (match(partIds[e]))
            {
                for (var v = 0; v < vertCount; v++)
                {
                    var i = (vertexBase + v) * stride;
                    sum += new Vector3(verts[i], verts[i + 1], verts[i + 2]);
                    count++;
                }
            }

            vertexBase += vertCount;
        }

        return count > 0 ? sum / count : null;
    }

    private static int CountEmittedFaces(ModelElement el)
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
}
