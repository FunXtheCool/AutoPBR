using System.Numerics;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EntityGpuShaderDiagnosticsTests
{
    [Fact]
    public void Bind_pose_palette_is_identity_on_bind_vertices()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var merged));

        var palette = EntityGpuShaderDiagnostics.BuildBindPoseBonePalette(merged);
        Assert.Equal(merged.Elements.Count, palette.Length);
        for (var i = 0; i < palette.Length; i++)
        {
            Assert.True(MatricesClose(palette[i], Matrix4x4.Identity, 1e-4f), $"bone {i} expected identity");
        }
    }

    [Fact]
    public void Draw_contract_warns_when_bind_mesh_zero_with_prepared_bones_anim_on()
    {
        var contract = EntityGpuShaderDiagnostics.BuildDrawContractSnapshot(
            gpuEntityBoneSkinning: true,
            preparedBoneCount: 10,
            vertexStrideFloats: 13,
            setupAnimMotion: true,
            boneSnapshotValid: true,
            boneSnapshotCount: 10,
            bonePaletteUploaded: true,
            resolveOk: true,
            uploadedGpuSkinning: 1,
            uploadedBoneCount: 10,
            uploadedLiftY: 0.01f,
            uploadedBindMesh: 0,
            uniformLocsComplete: true,
            entityBoneUboReady: true,
            verticesInPreviewSpace: false);
        var warn = EntityGpuShaderDiagnostics.FormatEntityDrawContractWarningLine("entity/cow.png", contract);
        Assert.Contains("uEntityBindMesh mismatch", warn, StringComparison.Ordinal);
        Assert.Contains("anim-on and anim-off", warn, StringComparison.Ordinal);
    }

    [Fact]
    public void Draw_contract_warns_when_anim_on_without_palette()
    {
        var contract = EntityGpuShaderDiagnostics.BuildDrawContractSnapshot(
            gpuEntityBoneSkinning: true,
            preparedBoneCount: 10,
            vertexStrideFloats: 13,
            setupAnimMotion: true,
            boneSnapshotValid: true,
            boneSnapshotCount: 10,
            bonePaletteUploaded: false,
            resolveOk: true,
            uploadedGpuSkinning: 1,
            uploadedBoneCount: 10,
            uploadedLiftY: 0.01f,
            uploadedBindMesh: 1,
            uniformLocsComplete: true,
            entityBoneUboReady: true,
            verticesInPreviewSpace: false);
        var warn = EntityGpuShaderDiagnostics.FormatEntityDrawContractWarningLine("entity/cow.png", contract);
        Assert.Contains("bone palette not uploaded", warn, StringComparison.Ordinal);
    }

    [Fact]
    public void Draw_contract_warns_on_vao_stride_mismatch()
    {
        var contract = EntityGpuShaderDiagnostics.BuildDrawContractSnapshot(
            gpuEntityBoneSkinning: true,
            preparedBoneCount: 10,
            vertexStrideFloats: 13,
            setupAnimMotion: false,
            boneSnapshotValid: true,
            boneSnapshotCount: 10,
            bonePaletteUploaded: true,
            resolveOk: true,
            uploadedGpuSkinning: 0,
            uploadedBoneCount: 10,
            uploadedLiftY: 0.01f,
            uploadedBindMesh: 1,
            uniformLocsComplete: true,
            entityBoneUboReady: true,
            verticesInPreviewSpace: false);
        var warn = EntityGpuShaderDiagnostics.FormatEntityDrawContractWarningLine("entity/cow.png", contract, uploadedMeshStrideFloats: 12);
        Assert.Contains("VAO stride=12", warn, StringComparison.Ordinal);
    }

    [Fact]
    public void Ler_basis_override_right_compose()
    {
        try
        {
            EntityPreviewDebugSettings.LerBasisOverride = EntityPreviewLerBasisOverride.RightComposeLocalChain;
            var basis = EntityModelRuntime.ResolveGeometryIrLerBasis(
                "net.minecraft.client.model.animal.cow.CowModel",
                "cow",
                normalizedAssetPath: null);
            Assert.Equal(
                EntityModelRuntime.GeometryIrLerBasisKind.RightComposeLocalChain,
                basis);
        }
        finally
        {
            EntityPreviewDebugSettings.LerBasisOverride = EntityPreviewLerBasisOverride.PolicyDefault;
        }
    }

    [Fact]
    public void Bone_index_histogram_warns_when_all_vertices_share_one_slot()
    {
        var verts = new float[MinecraftModelBaker.FloatsPerSkinnedVertex * 4];
        for (var v = 0; v < 4; v++)
        {
            var i = v * MinecraftModelBaker.FloatsPerSkinnedVertex;
            verts[i + 12] = BitConverter.Int32BitsToSingle(0);
        }

        var snap = EntityGpuShaderDiagnostics.BuildBoneIndexHistogram(verts, MinecraftModelBaker.FloatsPerSkinnedVertex, 10);
        Assert.Equal(4, snap.VertexCount);
        Assert.Equal(1, snap.DistinctBoneIndices);
        var warn = EntityGpuShaderDiagnostics.FormatBoneIndexHistogramWarningLine("entity/cow.png", snap, 10);
        Assert.Contains("all 4 bind verts use bone 0", warn, StringComparison.Ordinal);
    }

    [Fact]
    public void Bone_index_histogram_reports_multiple_slots()
    {
        var verts = new float[MinecraftModelBaker.FloatsPerSkinnedVertex * 3];
        for (var v = 0; v < 3; v++)
        {
            var i = v * MinecraftModelBaker.FloatsPerSkinnedVertex;
            verts[i + 12] = BitConverter.Int32BitsToSingle(v);
        }

        var snap = EntityGpuShaderDiagnostics.BuildBoneIndexHistogram(verts, MinecraftModelBaker.FloatsPerSkinnedVertex, 10);
        Assert.Equal(3, snap.DistinctBoneIndices);
        Assert.Equal(2, snap.MaxBoneIndex);
        var warn = EntityGpuShaderDiagnostics.FormatBoneIndexHistogramWarningLine("entity/cow.png", snap, 10);
        Assert.True(string.IsNullOrEmpty(warn));
    }

    [Fact]
    public void Runtime_snapshot_flags_exploded_bind_when_stride_wrong()
    {
        var snap = new EntityGpuShaderDiagnostics.RuntimeSnapshot(
            VertexStrideFloats: 12,
            VertexCount: 10,
            PreparedBoneCount: 0,
            BoneFillOk: false,
            BonePaletteUploaded: false,
            UploadedGpuSkinning: 0,
            UploadedBoneCount: 0,
            UploadedLiftY: 0f,
            UploadedBindMesh: 0,
            SimBodyCentroidY: 0f,
            SimLegCentroidY: 0f,
            SimHeadCentroidY: 0f,
            SimBodyLegGap: 0f,
            SimBodyHeadGap3D: 0f,
            SampleBodyBindY: 0f,
            SampleLegBindY: 0f,
            VerticesInPreviewSpace: false,
            BoneFillFailureHint: null);
        var warn = EntityGpuShaderDiagnostics.FormatExploreGpuRuntimeWarningLine("entity/cow.png", snap);
        Assert.Contains("stride=12", warn, StringComparison.Ordinal);
    }

    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps) =>
        MathF.Abs(a.M11 - b.M11) <= eps && MathF.Abs(a.M22 - b.M22) <= eps && MathF.Abs(a.M33 - b.M33) <= eps &&
        MathF.Abs(a.M44 - b.M44) <= eps && MathF.Abs(a.M41 - b.M41) <= eps && MathF.Abs(a.M42 - b.M42) <= eps &&
        MathF.Abs(a.M43 - b.M43) <= eps;
}
