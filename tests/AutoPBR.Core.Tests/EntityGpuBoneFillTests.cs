using System.Numerics;


namespace AutoPBR.Core.Tests;

public sealed class EntityGpuBoneFillTests
{
    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps)
    {
        return Math.Abs(a.M11 - b.M11) <= eps &&
               Math.Abs(a.M12 - b.M12) <= eps &&
               Math.Abs(a.M13 - b.M13) <= eps &&
               Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps &&
               Math.Abs(a.M22 - b.M22) <= eps &&
               Math.Abs(a.M23 - b.M23) <= eps &&
               Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps &&
               Math.Abs(a.M32 - b.M32) <= eps &&
               Math.Abs(a.M33 - b.M33) <= eps &&
               Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps &&
               Math.Abs(a.M42 - b.M42) <= eps &&
               Math.Abs(a.M43 - b.M43) <= eps &&
               Math.Abs(a.M44 - b.M44) <= eps;
    }

    [Fact]
    public void TryFillBoneMatricesFast_matches_TryBuildStaticMesh_for_catalogued_cow()
    {
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/cow/cow.png";
        var absent = TestEnvironmentPaths.AbsentNativeRoot;
        var profile = new MinecraftNativeProfile("26.1.2", absent, new Version(26, 1, 2));
        const float idle = 0.37f;
        const float anim = 1.91f;

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, anim, out var full));
        var scratch = new List<Matrix4x4>(128);
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount));
        Assert.Equal(full.Elements.Count, boneCount);
        Assert.Equal(scratch.Count, boneCount);
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(MatricesClose(full.Elements[i].LocalToParent, scratch[i], 1e-4f), $"bone index {i}");
        }
    }

    [Fact]
    public void TryFillBoneMatricesFast_sets_parity_route_cache_on_rebake_context()
    {
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        var absent = TestEnvironmentPaths.AbsentNativeRoot;
        var profile = new MinecraftNativeProfile("26.1.2", absent, new Version(26, 1, 2));
        const float idle = 0.11f;
        const float anim = 0.55f;

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = absent,
            NativeProfileName = profile.Name,
            NativeParsedVersion = profile.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idle,
            OrderedTextureZipPaths = [path]
        };

        var scratch = new List<Matrix4x4>(128);
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount, rebake));
        Assert.True(boneCount > 0);
        Assert.NotNull(rebake.GpuBoneDispatchRoute);
        Assert.Equal(EntityGpuBoneDispatchKind.ParityCatalog, rebake.GpuBoneDispatchRoute!.Value.Kind);
        Assert.False(string.IsNullOrEmpty(rebake.GpuBoneDispatchRoute.Value.ParityBuilderMethod));
    }

    [Fact]
    public void TryFillBoneMatricesFast_family_fallback_route_cache_matches_full_dispatch()
    {
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/custom/zzzevokerfang.png";
        var absent = TestEnvironmentPaths.AbsentNativeRoot;
        var profile = new MinecraftNativeProfile("26.1.2", absent, new Version(26, 1, 2));
        const float idle = 0.21f;
        const float anim = 0.88f;

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = absent,
            NativeProfileName = profile.Name,
            NativeParsedVersion = profile.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idle,
            OrderedTextureZipPaths = [path]
        };

        var scratch = new List<Matrix4x4>(128);
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount1, rebake));
        Assert.Equal(EntityGpuBoneDispatchKind.FamilyFallback, rebake.GpuBoneDispatchRoute!.Value.Kind);
        Assert.Equal(EntityGpuBoneFamily.Humanoid, rebake.GpuBoneDispatchRoute.Value.Family);

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, anim, out var full));
        Assert.Equal(full.Elements.Count, boneCount1);

        scratch.Clear();
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount2, rebake));
        Assert.Equal(boneCount1, boneCount2);
        for (var i = 0; i < boneCount2; i++)
        {
            Assert.True(MatricesClose(full.Elements[i].LocalToParent, scratch[i], 1e-4f), $"bone index {i}");
        }
    }

    [Fact]
    public void TryFillBoneMatricesFast_specific_model_slot_route_cache_matches_full_dispatch_for_pig()
    {
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/pig/pig.png";
        var absent = TestEnvironmentPaths.AbsentNativeRoot;
        var profile = new MinecraftNativeProfile("26.1.2", absent, new Version(26, 1, 2));
        const float idle = 0.31f;
        const float anim = 2.02f;

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = absent,
            NativeProfileName = profile.Name,
            NativeParsedVersion = profile.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idle,
            OrderedTextureZipPaths = [path]
        };

        var scratch = new List<Matrix4x4>(128);
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount1, rebake));
        Assert.Equal(EntityGpuBoneDispatchKind.SpecificModelSlot, rebake.GpuBoneDispatchRoute!.Value.Kind);
        Assert.True(rebake.GpuBoneDispatchRoute.Value.SpecificSlot > 0);

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, anim, out var full));
        Assert.Equal(full.Elements.Count, boneCount1);

        scratch.Clear();
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount2, rebake));
        Assert.Equal(boneCount1, boneCount2);
        for (var i = 0; i < boneCount2; i++)
        {
            Assert.True(MatricesClose(full.Elements[i].LocalToParent, scratch[i], 1e-4f), $"bone index {i}");
        }
    }

    [Fact]
    public void RequiresFullMeshBoneExtract_includes_chicken_entity_diffuse_paths()
    {
        Assert.True(EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract("assets/minecraft/textures/entity/chicken/chicken.png"));
        Assert.True(EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract("assets/minecraft/textures/entity/chicken/chicken_cold.png"));
        Assert.False(EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract("assets/minecraft/textures/entity/cow/cow.png"));
    }

    [Fact]
    public void TryFillEmulatedEntityBoneMatrices_full_extract_matches_TryBuildStaticMesh_for_chicken_cold()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/chicken/chicken_cold.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        const float idle = 0.27f;
        const float anim = 1.41f;

        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 0f, out var bindMerged));
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            profile,
            idle,
            anim,
            out var full,
            applyGeometryIrSetupAnimMotion: true));
        var n = full.Elements.Count;
        Assert.True(n > 0);

        var inv = new Matrix4x4[n];
        for (var i = 0; i < n; i++)
        {
            Assert.True(Matrix4x4.Invert(bindMerged.Elements[i].LocalToParent, out inv[i]), $"invert bind bone {i}");
        }

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = profile.Name,
            NativeParsedVersion = profile.ParsedVersion?.ToString(),
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idle,
            OrderedTextureZipPaths = [path],
            GpuPreparedBoneCount = n,
            GpuBindPoseInverseLocalToParent = inv,
        };

        Span<Matrix4x4> bones = stackalloc Matrix4x4[EntityGpuSkinningLimits.MaxBones];
        Assert.True(EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(rebake, anim, bones, out var boneCount));
        Assert.Equal(n, boneCount);
        for (var i = 0; i < n; i++)
        {
            var expected = Matrix4x4.Multiply(inv[i], full.Elements[i].LocalToParent);
            Assert.True(MatricesClose(expected, bones[i], 1e-4f), $"bone index {i}");
        }
    }
}
