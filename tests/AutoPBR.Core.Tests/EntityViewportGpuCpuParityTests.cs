using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EntityViewportGpuCpuParityTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Theory]
    [InlineData(
        "assets/minecraft/textures/entity/cow/cow_temperate.png",
        "net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData(
        "assets/minecraft/textures/entity/hoglin/hoglin.png",
        "net.minecraft.client.model.monster.hoglin.HoglinModel")]
    public void Catalog_gpu_bone_fill_matches_cpu_geometry_ir_mesh_at_same_clock(
        string texturePath,
        string expectedJvm)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const float idlePhase = 0.25f;
        const float t = 1.75f;

        Assert.True(runtime.TryBuildStaticMesh(
            texturePath,
            Profile26,
            idlePhase,
            t,
            out var cpuMesh,
            out var provenance,
            applyGeometryIrSetupAnimMotion: true));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(expectedJvm, provenance.Detail ?? "", StringComparison.Ordinal);

        var bones = new List<Matrix4x4>();
        var rebake = CreateRebakeContext(texturePath);
        Assert.True(runtime.TryFillBoneMatricesFast(texturePath, Profile26, idlePhase, t, bones, out var boneCount, rebake));

        Assert.Equal(cpuMesh.Elements.Count, boneCount);
        Assert.Equal(cpuMesh.Elements.Count, bones.Count);
        Assert.Equal(EntityGpuBoneDispatchKind.ParityCatalog, rebake.GpuBoneDispatchRoute?.Kind);
        Assert.Equal(expectedJvm, rebake.GpuBoneDispatchRoute?.GeometryIrOfficialJvm);
        for (var i = 0; i < cpuMesh.Elements.Count; i++)
        {
            AssertMatrixClose(cpuMesh.Elements[i].LocalToParent, bones[i], tolerance: 1e-5f);
        }
    }

    [Fact]
    public void Bind_pose_ground_lift_is_stable_for_skinned_entity_vertices()
    {
        ReadOnlySpan<float> skinned =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 16f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
        ];

        var lift = EntityEmulatedGpuSkinningMath.ComputeMeshSpaceGroundLift(
            skinned,
            floorY: -0.56f,
            clearance: 0.002f);

        // The lowest bind vertex normalizes to y=-0.5, already above the grid target (-0.558).
        Assert.Equal(0f, lift);
    }

    private static EntityEmulatedPreviewRebakeContext CreateRebakeContext(string texturePath) =>
        new()
        {
            PackZipPath = "unused.zip",
            AssetArchivePath = texturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.25f,
            OrderedTextureZipPaths = []
        };

    private static void AssertMatrixClose(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
    {
        ReadOnlySpan<float> e =
        [
            expected.M11, expected.M12, expected.M13, expected.M14,
            expected.M21, expected.M22, expected.M23, expected.M24,
            expected.M31, expected.M32, expected.M33, expected.M34,
            expected.M41, expected.M42, expected.M43, expected.M44,
        ];
        ReadOnlySpan<float> a =
        [
            actual.M11, actual.M12, actual.M13, actual.M14,
            actual.M21, actual.M22, actual.M23, actual.M24,
            actual.M31, actual.M32, actual.M33, actual.M34,
            actual.M41, actual.M42, actual.M43, actual.M44,
        ];
        for (var i = 0; i < e.Length; i++)
        {
            Assert.True(MathF.Abs(e[i] - a[i]) <= tolerance, $"matrix[{i}] expected {e[i]:F6}, got {a[i]:F6}");
        }
    }
}
