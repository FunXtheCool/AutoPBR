using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

public sealed class EntityPreviewPoseCatalogTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/illager/evoker.png", "Evoker", EntityIllagerPreviewArmPose.Crossed)]
    [InlineData("assets/minecraft/textures/entity/illager/vindicator.png", "Vindicator", EntityIllagerPreviewArmPose.Crossed)]
    [InlineData("assets/minecraft/textures/entity/illager/pillager.png", "Pillager", EntityIllagerPreviewArmPose.ArmsAtSide)]
    [InlineData("assets/minecraft/textures/entity/illager/illusioner.png", "Illager", EntityIllagerPreviewArmPose.Crossed)]
    public void ResolveEffectiveIllagerArmPose_uses_texture_default_when_no_selector(
        string path,
        string builderMethod,
        EntityIllagerPreviewArmPose expected)
    {
        var pose = EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(path, builderMethod, selectedPoseId: null);
        Assert.Equal(expected, pose);
    }

    [Fact]
    public void TryGetPoseOptions_returns_all_illager_arm_poses_for_evoker()
    {
        const string path = "assets/minecraft/textures/entity/illager/evoker.png";
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(path, "Evoker", out var options));
        Assert.Equal(8, options.Count);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.IllagerCrossed);
    }

    [Fact]
    public void TryGetPoseOptions_excludes_crossed_arms_for_pillager()
    {
        const string path = "assets/minecraft/textures/entity/illager/pillager.png";
        Assert.True(EntityPreviewPoseCatalog.TryGetPoseOptions(path, "Pillager", out var options));
        Assert.Equal(8, options.Count);
        Assert.DoesNotContain(options, o => o.Id == EntityPreviewPoseCatalog.IllagerCrossed);
        Assert.Single(options, o => o.IsDefault && o.Id == EntityPreviewPoseCatalog.IllagerArmsAtSide);
    }

    [Fact]
    public void ResolveEffectiveIllagerArmPose_maps_crossed_to_arms_at_side_for_pillager()
    {
        const string path = "assets/minecraft/textures/entity/illager/pillager.png";
        var pose = EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(
            path,
            "Pillager",
            EntityPreviewPoseCatalog.IllagerCrossed);
        Assert.Equal(EntityIllagerPreviewArmPose.ArmsAtSide, pose);
    }
}

public sealed class IllagerPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string EvokerPath = "assets/minecraft/textures/entity/illager/evoker.png";
    private const string PillagerPath = "assets/minecraft/textures/entity/illager/pillager.png";
    private const string IllusionerPath = "assets/minecraft/textures/entity/illager/illusioner.png";

    [Fact]
    public void Pillager_arms_at_side_rebake_resolves_separate_arm_part_ids_and_visible_bounds()
    {
        var profile = new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerArmsAtSide))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PillagerPath,
                profile,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var merged,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(8, merged.Elements.Count);

            var rebake = new EntityEmulatedPreviewRebakeContext
            {
                PackZipPath = "test.zip",
                AssetArchivePath = PillagerPath,
                NativeRootDirectory = profile.RootDirectory,
                NativeProfileName = profile.Name,
                NativeParsedVersion = profile.ParsedVersion?.ToString(),
                ModelDefaultNamespace = "minecraft",
                IdlePhase01 = 0f,
                PreviewPoseId = EntityPreviewPoseCatalog.IllagerArmsAtSide,
                OrderedTextureZipPaths = [PillagerPath],
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
                out var verts,
                out _,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.NotNull(rebake.ElementPartIds);
            Assert.Equal(8, rebake.ElementPartIds!.Length);
            Assert.Contains(rebake.ElementPartIds, id => string.Equals(id, "left_arm", StringComparison.Ordinal));
            Assert.Contains(rebake.ElementPartIds, id => string.Equals(id, "right_arm", StringComparison.Ordinal));
            Assert.DoesNotContain(rebake.ElementPartIds, id => string.Equals(id, "arms", StringComparison.Ordinal));

            var bounds = MeasureCpuPreviewBoundsByPart(verts!, merged, rebake.ElementPartIds);
            Assert.True(bounds.TryGetValue("left_arm", out var leftArmBounds));
            Assert.True(bounds.TryGetValue("right_arm", out var rightArmBounds));
            Assert.True(leftArmBounds.SpanY > 0.15f, $"left_arm spanY={leftArmBounds.SpanY:F3}");
            Assert.True(rightArmBounds.SpanY > 0.15f, $"right_arm spanY={rightArmBounds.SpanY:F3}");
        }
    }

    [Fact]
    public void Pillager_default_runtime_mesh_uses_separate_arms()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            PillagerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(8, mesh.Elements.Count);
    }

    [Fact]
    public void Evoker_crossed_and_pillager_arms_at_side_use_different_element_counts()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            EvokerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var evoker,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.True(runtime.TryBuildStaticMesh(
            PillagerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var pillager,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(9, evoker.Elements.Count);
        Assert.Equal(8, pillager.Elements.Count);
    }

    [Fact]
    public void TryPopulateRebakeElementPartIds_refreshes_when_crossed_pose_element_count_changes()
    {
        var profile = new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2));
        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = EvokerPath,
            NativeRootDirectory = profile.RootDirectory,
            NativeProfileName = profile.Name,
            NativeParsedVersion = profile.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            PreviewPoseId = EntityPreviewPoseCatalog.IllagerSpellcasting,
            OrderedTextureZipPaths = [EvokerPath],
        };

        EntityPreviewPlacement.TryPopulateRebakeElementPartIds(rebake, profile, elementCount: 8);
        Assert.Equal(8, rebake.ElementPartIds!.Length);
        Assert.DoesNotContain(rebake.ElementPartIds, id => string.Equals(id, "arms", StringComparison.Ordinal));

        rebake.ElementPartIds =
        [
            "head", "nose", "body", "body", "right_arm", "left_arm", "right_leg", "left_leg",
        ];
        rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = rebake.PackZipPath,
            AssetArchivePath = rebake.AssetArchivePath,
            NativeRootDirectory = rebake.NativeRootDirectory,
            NativeProfileName = rebake.NativeProfileName,
            NativeParsedVersion = rebake.NativeParsedVersion,
            ModelDefaultNamespace = rebake.ModelDefaultNamespace,
            IdlePhase01 = rebake.IdlePhase01,
            PreviewPoseId = EntityPreviewPoseCatalog.IllagerCrossed,
            OrderedTextureZipPaths = rebake.OrderedTextureZipPaths,
            ElementPartIds = rebake.ElementPartIds,
        };
        EntityPreviewPlacement.TryPopulateRebakeElementPartIds(rebake, profile, elementCount: 9);
        Assert.Equal(9, rebake.ElementPartIds!.Length);
        Assert.Contains(rebake.ElementPartIds, id => string.Equals(id, "arms", StringComparison.Ordinal));
        Assert.Contains(rebake.ElementPartIds, id => string.Equals(id, "left_shoulder", StringComparison.Ordinal));
    }

    [Fact]
    public void Evoker_default_runtime_mesh_has_no_duplicate_arm_cuboids()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            EvokerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Equal(9, mesh.Elements.Count);
    }

    [Fact]
    public void Evoker_spellcasting_selector_pose_uses_separate_arms_with_setup_anim()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerSpellcasting))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                EvokerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 1.5f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(8, mesh.Elements.Count);
        }
    }

    [Fact]
    public void Illusioner_default_runtime_mesh_uses_folded_arms()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            IllusionerPath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var mesh,
            out _,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(9, mesh.Elements.Count);
    }

    [Fact]
    public void Pillager_arms_at_side_runtime_mesh_uses_separate_arms_with_setup_anim()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerArmsAtSide))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PillagerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 1.5f,
                out var mesh,
                out var provenance,
                applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
            Assert.Equal(8, mesh.Elements.Count);
        }
    }

    [Fact]
    public void Pillager_rebake_bone_fill_uses_stored_pose_without_async_scope()
    {
        var profile = new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2));
        const float idle = 0f;
        const float anim = 1.5f;
        var runtime = EntityModelRuntimeFactory.Create();

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerArmsAtSide))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                PillagerPath,
                profile,
                idle,
                0f,
                out var bindMerged,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(8, bindMerged.Elements.Count);
            Assert.True(runtime.TryBuildStaticMesh(
                PillagerPath,
                profile,
                idle,
                anim,
                out var animMerged,
                applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(8, animMerged.Elements.Count);

            var inv = new Matrix4x4[bindMerged.Elements.Count];
            for (var i = 0; i < inv.Length; i++)
            {
                Assert.True(Matrix4x4.Invert(bindMerged.Elements[i].LocalToParent, out inv[i]));
            }

            var rebake = new EntityEmulatedPreviewRebakeContext
            {
                PackZipPath = "test.zip",
                AssetArchivePath = PillagerPath,
                NativeRootDirectory = profile.RootDirectory,
                NativeProfileName = profile.Name,
                NativeParsedVersion = profile.ParsedVersion?.ToString(),
                ModelDefaultNamespace = "minecraft",
                IdlePhase01 = idle,
                PreviewPoseId = EntityPreviewPoseCatalog.IllagerArmsAtSide,
                OrderedTextureZipPaths = [PillagerPath],
                GpuPreparedBoneCount = bindMerged.Elements.Count,
                GpuBindPoseInverseLocalToParent = inv,
            };

            Span<Matrix4x4> bones = stackalloc Matrix4x4[EntityGpuSkinningLimits.MaxBones];
            Assert.True(EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                rebake,
                anim,
                bones,
                out var boneCount,
                applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(8, boneCount);
        }
    }

    [Fact]
    public void Evoker_spellcasting_and_crossed_selector_produce_different_static_meshes()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerCrossed))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                EvokerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var crossed,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(9, crossed.Elements.Count);
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerSpellcasting))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                EvokerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var spellcasting,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(8, spellcasting.Elements.Count);
        }
    }

    [Fact]
    public void Evoker_crossed_selector_pose_switches_to_folded_arms()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.IllagerCrossed))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                EvokerPath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: false));
            Assert.Equal(9, mesh.Elements.Count);
        }
    }

    private readonly record struct PartPreviewBounds(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)
    {
        public float SpanX => MaxX - MinX;
        public float SpanY => MaxY - MinY;
        public float SpanZ => MaxZ - MinZ;

        public static PartPreviewBounds FromPoint(Vector3 p) => new(p.X, p.Y, p.Z, p.X, p.Y, p.Z);

        public PartPreviewBounds Include(Vector3 p) => new(
            MathF.Min(MinX, p.X),
            MathF.Min(MinY, p.Y),
            MathF.Min(MinZ, p.Z),
            MathF.Max(MaxX, p.X),
            MathF.Max(MaxY, p.Y),
            MathF.Max(MaxZ, p.Z));

        public PartPreviewBounds Union(PartPreviewBounds other) => new(
            MathF.Min(MinX, other.MinX),
            MathF.Min(MinY, other.MinY),
            MathF.Min(MinZ, other.MinZ),
            MathF.Max(MaxX, other.MaxX),
            MathF.Max(MaxY, other.MaxY),
            MathF.Max(MaxZ, other.MaxZ));
    }

    private static Dictionary<string, PartPreviewBounds> MeasureCpuPreviewBoundsByPart(
        ReadOnlySpan<float> vertices,
        MergedJavaBlockModel model,
        string[] partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var result = new Dictionary<string, PartPreviewBounds>(StringComparer.Ordinal);
        var floatOffset = 0;
        var elementCount = Math.Min(model.Elements.Count, partIds.Length);
        for (var ei = 0; ei < elementCount; ei++)
        {
            var vertexCount = model.Elements[ei].Faces.Count * 4;
            PartPreviewBounds? bounds = null;
            for (var vi = 0; vi < vertexCount && floatOffset + stride - 1 < vertices.Length; vi++, floatOffset += stride)
            {
                var p = new Vector3(vertices[floatOffset], vertices[floatOffset + 1], vertices[floatOffset + 2]);
                bounds = bounds is { } existing ? existing.Include(p) : PartPreviewBounds.FromPoint(p);
            }

            if (bounds is { } measured)
            {
                var id = partIds[ei];
                result[id] = result.TryGetValue(id, out var aggregate)
                    ? aggregate.Union(measured)
                    : measured;
            }
        }

        return result;
    }
}
