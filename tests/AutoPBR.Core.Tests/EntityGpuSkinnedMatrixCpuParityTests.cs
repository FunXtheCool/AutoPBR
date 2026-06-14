using System.Numerics;



namespace AutoPBR.Core.Tests;

/// <summary>
/// GPU bind-pose vertices store <c>r · M_bind</c> (pre <c>x/16−½</c>); uniforms use <c>M_bind⁻¹ · M_anim</c> (row) so
/// <c>r · M_bind · bone = r · M_anim</c>; the vertex shader applies the same <c>x/16−½</c> as <see cref="MinecraftModelBaker"/>.
/// CPU <see cref="MinecraftModelBaker.TryBake"/> emits <c>W(M_anim · r)</c>.
/// </summary>
public sealed class EntityGpuSkinnedMatrixCpuParityTests
{
    private static int DecodeBoneWord(float f) => BitConverter.SingleToInt32Bits(f);

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps)
    {
        return Math.Abs(a.M11 - b.M11) <= eps && Math.Abs(a.M12 - b.M12) <= eps && Math.Abs(a.M13 - b.M13) <= eps &&
               Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps && Math.Abs(a.M22 - b.M22) <= eps && Math.Abs(a.M23 - b.M23) <= eps &&
               Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps && Math.Abs(a.M32 - b.M32) <= eps && Math.Abs(a.M33 - b.M33) <= eps &&
               Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps && Math.Abs(a.M43 - b.M43) <= eps &&
               Math.Abs(a.M44 - b.M44) <= eps;
    }

    private static Matrix4x4[] InvertBindPoses(MergedJavaBlockModel bindMerged)
    {
        var n = bindMerged.Elements.Count;
        var inv = new Matrix4x4[n];
        for (var i = 0; i < n; i++)
        {
            if (!Matrix4x4.Invert(bindMerged.Elements[i].LocalToParent, out inv[i]))
            {
                inv[i] = Matrix4x4.Identity;
            }
        }

        return inv;
    }

    private static void BakeCpuAndGpuBind(
        string assetPath,
        float idle,
        float anim,
        out float[] cpuVerts,
        out float[] gpuVerts,
        out Matrix4x4[] invBind,
        out MergedJavaBlockModel mergedAnim,
        bool applyGeometryIrSetupAnimMotion = false)
    {
        var profile = assetPath.Contains("/camel/", StringComparison.OrdinalIgnoreCase)
            ? new MinecraftNativeProfile(
                "26.1.2",
                Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"),
                new Version(26, 1, 2))
            : new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(assetPath, profile, idle, 0f, out var mergedBind));
        Assert.True(runtime.TryBuildStaticMesh(
            assetPath,
            profile,
            idle,
            anim,
            out mergedAnim,
            applyGeometryIrSetupAnimMotion: applyGeometryIrSetupAnimMotion));

        invBind = InvertBindPoses(mergedBind);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedBind, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 32);
        }

        Assert.True(MinecraftModelBaker.TryBake(
            mergedAnim,
            "minecraft",
            pathToIdx,
            texSizes,
            out cpuVerts!,
            out _,
            out _));
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            mergedBind,
            "minecraft",
            pathToIdx,
            texSizes,
            out gpuVerts!,
            out _,
            out _));
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken.png", 0.27f, 1.41f)]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_cold.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_cold.png", 0.27f, 1.41f)]
    [InlineData("assets/minecraft/textures/entity/camel/camel.png", 0.3f, 1.25f)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 0.2f, 1.1f)]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", 0.15f, 0.9f)]
    [InlineData("assets/minecraft/textures/entity/horse/horse_black_baby.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", 0.2f, 1.1f)]
    [InlineData("assets/minecraft/textures/entity/fox/fox_baby.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/cat/cat_british_shorthair_baby.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/goat/goat_baby.png", 0f, 0f)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", 0f, 2.5f)]
    [InlineData("assets/minecraft/textures/entity/horse/donkey.png", 0.2f, 3.1f)]
    [InlineData("assets/minecraft/textures/entity/dolphin/dolphin.png", 0.3f, 2.5f)]
    public void Skinned_bind_pose_vertices_with_M_anim_inv_then_component_W_match_cpu_bake(string assetPath, float idle, float anim)
    {
        var setupAnim = assetPath.Contains("/camel/", StringComparison.OrdinalIgnoreCase) ||
            assetPath.Contains("/cow/", StringComparison.OrdinalIgnoreCase) ||
            assetPath.Contains("/panda/", StringComparison.OrdinalIgnoreCase) ||
            assetPath.Contains("/dolphin/", StringComparison.OrdinalIgnoreCase);
        BakeCpuAndGpuBind(assetPath, idle, anim, out var cpuVerts, out var gpuVerts, out var invBind, out var mergedAnim, setupAnim);
        const int cpuStride = MinecraftModelBaker.FloatsPerVertex;
        const int gpuStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var nCpu = cpuVerts.Length / cpuStride;
        var nGpu = gpuVerts.Length / gpuStride;
        Assert.Equal(nCpu, nGpu);

        const float eps = 2e-4f;
        for (var vi = 0; vi < nCpu; vi++)
        {
            var bi = DecodeBoneWord(gpuVerts[vi * gpuStride + 12]);
            Assert.InRange(bi, 0, invBind.Length - 1);
            var pBind = new Vector3(gpuVerts[vi * gpuStride], gpuVerts[vi * gpuStride + 1], gpuVerts[vi * gpuStride + 2]);
            var m = mergedAnim.Elements[bi].LocalToParent;
            var inv = invBind[bi];
            var texel = Vector3.Transform(Vector3.Transform(pBind, inv), m);
            var actual = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(texel);
            var expected = new Vector3(cpuVerts[vi * cpuStride], cpuVerts[vi * cpuStride + 1], cpuVerts[vi * cpuStride + 2]);
            Assert.True(
                Vector3.Distance(expected, actual) <= eps,
                $"vertex {vi} bone {bi} path {assetPath}: expected {expected}, got {actual} (idle={idle}, anim={anim})");
        }
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 0.2f, 1.1f, false)]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", 0.2f, 1.1f, true)]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", 0.15f, 0.9f, false)]
    [InlineData("assets/minecraft/textures/entity/dolphin/dolphin.png", 0.3f, 2.5f, true)]
    public void Parity_catalog_gpu_bones_respect_setup_anim_motion_flag(
        string path,
        float idle,
        float anim,
        bool setupAnimMotion)
    {
        var profile = new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 0f, out var bindMerged));
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            profile,
            idle,
            anim,
            out var animMerged,
            applyGeometryIrSetupAnimMotion: setupAnimMotion));
        var n = animMerged.Elements.Count;
        var inv = InvertBindPoses(bindMerged);
        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = profile.RootDirectory,
            NativeProfileName = profile.Name,
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = idle,
            OrderedTextureZipPaths = [path],
            GpuPreparedBoneCount = n,
            GpuBindPoseInverseLocalToParent = inv,
        };

        Span<Matrix4x4> bones = stackalloc Matrix4x4[EntityGpuSkinningLimits.MaxBones];
        Assert.True(EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
            rebake,
            anim,
            bones,
            out var boneCount,
            applyGeometryIrSetupAnimMotion: setupAnimMotion));
        Assert.Equal(n, boneCount);
        for (var i = 0; i < n; i++)
        {
            var expected = Matrix4x4.Multiply(inv[i], animMerged.Elements[i].LocalToParent);
            Assert.True(MatricesClose(expected, bones[i], 1e-5f), $"bone {i} path {path} setupAnim={setupAnimMotion}");
        }
    }

    [Fact]
    public void Emulated_rebake_bone_snapshot_matches_inv_times_M_for_chicken_cold()
    {
        const string path = "assets/minecraft/textures/entity/chicken/chicken_cold.png";
        var profile = new MinecraftNativeProfile("26.1.2", AppContext.BaseDirectory, new Version(26, 1, 2));
        const float idle = 0.11f;
        const float anim = 0.73f;

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, 0f, out var bindMerged));
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            profile,
            idle,
            anim,
            out var animMerged,
            applyGeometryIrSetupAnimMotion: true));
        var n = animMerged.Elements.Count;
        var inv = InvertBindPoses(bindMerged);

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "test.zip",
            AssetArchivePath = path,
            NativeRootDirectory = profile.RootDirectory,
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
            var expected = Matrix4x4.Multiply(inv[i], animMerged.Elements[i].LocalToParent);
            Assert.True(MatricesClose(expected, bones[i], 1e-5f), $"bone {i} inv·M vs rebake snapshot");
        }
    }

}
