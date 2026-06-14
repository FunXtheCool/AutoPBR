using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    private const string DolphinTexturePath = "assets/minecraft/textures/entity/dolphin/dolphin.png";

    [Fact]
    public void SetBlockModelPreview_reuses_committed_parity_cpu_subject_on_ui_repush()
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateDolphinTextureMaps();
        var slotMaterials = CreateDolphinSlotMaterials(textureMaps);
        var rebake = CreateDolphinRebakeContext(packFingerprint: 1001UL);

        var committedVerts = CreatePreviewVerts(192, seed: 1);
        var packVerts = CreatePreviewVerts(192, seed: 2);
        var indices = CreatePreviewIndices(288);

        var committedSubject = CreateDolphinSubject(
            committedVerts,
            indices,
            textureMaps,
            rebake,
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        var repushRebake = CreateDolphinRebakeContext(packFingerprint: 1001UL);
        var packSubject = CreateDolphinSubject(
            packVerts,
            indices,
            textureMaps,
            repushRebake,
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(committedVerts, stored!.InterleavedVertices);
        Assert.Same(indices, stored.Indices);
        Assert.False(stored.GpuEntityBoneSkinning);
        Assert.True(stored.EntityGpuVerticesInPreviewSpace);
        Assert.True(stored.EntityPreviewPlacementApplied);
        Assert.False(backend.TestMeshDirty);
        Assert.NotNull(backend.TestEntityBindPoseCommittedKey);
        Assert.Equal(
            OpenGlPreviewBackend.TestBuildParityCatalogCpuBindCommitKey(rebake),
            backend.TestEntityBindPoseCommittedKey);
    }

    [Fact]
    public void Material_refresh_preserves_committed_parity_cpu_subject_before_ui_repush()
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateDolphinTextureMaps();
        var slotMaterials = CreateDolphinSlotMaterials(textureMaps);
        var rebake = CreateDolphinRebakeContext(packFingerprint: 1101UL);

        var committedVerts = CreatePreviewVerts(192, seed: 21);
        var packVerts = CreatePreviewVerts(192, seed: 22);
        var indices = CreatePreviewIndices(288);

        var committedSubject = CreateDolphinSubject(
            committedVerts,
            indices,
            textureMaps,
            rebake,
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        // GlPbrPreviewControl.UpdatePreview3D refreshes the primary material before it re-pushes
        // the Java/entity subject. Keep the committed TryRebakeMesh subject alive across that step.
        backend.SetMaterial(slotMaterials[0]);

        var packSubject = CreateDolphinSubject(
            packVerts,
            indices,
            textureMaps,
            CreateDolphinRebakeContext(packFingerprint: 1101UL),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(committedVerts, stored!.InterleavedVertices);
        Assert.True(stored.EntityGpuVerticesInPreviewSpace);
        Assert.True(stored.EntityPreviewPlacementApplied);
        Assert.False(backend.TestMeshDirty);
        Assert.Equal(
            OpenGlPreviewBackend.TestBuildParityCatalogCpuBindCommitKey(rebake),
            backend.TestEntityBindPoseCommittedKey);
    }

    [Fact]
    public void SetBlockModelPreview_invalidates_parity_commit_only_when_pack_fingerprint_changes()
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateDolphinTextureMaps();
        var slotMaterials = CreateDolphinSlotMaterials(textureMaps);
        var committedVerts = CreatePreviewVerts(192, seed: 10);
        var indices = CreatePreviewIndices(288);

        var committedSubject = CreateDolphinSubject(
            committedVerts,
            indices,
            textureMaps,
            CreateDolphinRebakeContext(packFingerprint: 2001UL),
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);
        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);
        var committedKey = backend.TestEntityBindPoseCommittedKey;
        Assert.NotNull(committedKey);

        var sameFpSubject = CreateDolphinSubject(
            CreatePreviewVerts(192, seed: 99),
            indices,
            textureMaps,
            CreateDolphinRebakeContext(packFingerprint: 2001UL),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);
        backend.SetBlockModelPreview(sameFpSubject, slotMaterials);
        Assert.Equal(committedKey, backend.TestEntityBindPoseCommittedKey);
        Assert.Same(committedVerts, backend.TestBlockModelSubject!.InterleavedVertices);
        Assert.False(backend.TestMeshDirty);

        var newFpSubject = CreateDolphinSubject(
            CreatePreviewVerts(192, seed: 100),
            indices,
            textureMaps,
            CreateDolphinRebakeContext(packFingerprint: 2002UL),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);
        backend.SetBlockModelPreview(newFpSubject, slotMaterials);
        Assert.Null(backend.TestEntityBindPoseCommittedKey);
        Assert.True(backend.TestMeshDirty);
    }

    [Fact]
    public void Cpu_placed_parity_entity_resolves_preview_space_draw_uniforms()
    {
        var subject = CreateDolphinSubject(
            CreatePreviewVerts(192, seed: 3),
            CreatePreviewIndices(288),
            CreateDolphinTextureMaps(),
            CreateDolphinRebakeContext(packFingerprint: 3001UL),
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        var ok = OpenGlPreviewBackend.TestTryResolveEntitySkinningDrawState(
            subject,
            meshSpaceLiftY: 0.5f,
            boneSnapshotValid: true,
            boneSnapshotCount: 8,
            setupAnimMotion: false,
            out var previewSpaceVerts,
            out var bindMesh,
            out var gpuSkinning,
            out var boneCount,
            out var liftY);

        Assert.True(ok);
        Assert.Equal(1f, previewSpaceVerts);
        Assert.Equal(0f, bindMesh);
        Assert.Equal(0, gpuSkinning);
        Assert.Equal(0, boneCount);
        Assert.Equal(0f, liftY);
    }

    private static PreviewTextureMaps[] CreateDolphinTextureMaps() =>
    [
        new()
        {
            Width = 64,
            Height = 64,
            DiffuseRgba = new byte[64 * 64 * 4],
            NormalRgba = new byte[64 * 64 * 4],
            SpecularRgba = new byte[64 * 64 * 4],
            HeightRgba = new byte[64 * 64 * 4],
        }
    ];

    private static PreviewMaterial[] CreateDolphinSlotMaterials(PreviewTextureMaps[] textureMaps) =>
        textureMaps.Select(PreviewMaterialMapper.FromCoreMaps).ToArray();

    private static EntityEmulatedPreviewRebakeContext CreateDolphinRebakeContext(ulong packFingerprint) =>
        new()
        {
            PackZipPath = "minecraft-26.1.2-client.jar",
            AssetArchivePath = DolphinTexturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.3f,
            OrderedTextureZipPaths = [DolphinTexturePath],
            PackConverterCpuMeshFingerprint = packFingerprint,
        };

    private static PreviewModelSubject CreateDolphinSubject(
        float[] verts,
        uint[] indices,
        PreviewTextureMaps[] materials,
        EntityEmulatedPreviewRebakeContext rebake,
        bool entityGpuVerticesInPreviewSpace,
        bool entityPreviewPlacementApplied) =>
        new()
        {
            InterleavedVertices = verts,
            Indices = indices,
            DrawBatches = [new PreviewDrawBatch(0, indices.Length, 0)],
            Materials = materials,
            PrimaryMaterialIndex = 0,
            AnimationPreset = "entity_emulated",
            EmulatedRebake = rebake,
            GpuEntityBoneSkinning = false,
            VertexStrideFloats = 0,
            EntityGpuVerticesInPreviewSpace = entityGpuVerticesInPreviewSpace,
            EntityPreviewPlacementApplied = entityPreviewPlacementApplied,
        };

    private static float[] CreatePreviewVerts(int vertexCount, int seed)
    {
        const int stride = PreviewMesh.FloatsPerVertex;
        var verts = new float[vertexCount * stride];
        for (var v = 0; v < vertexCount; v++)
        {
            var o = v * stride;
            verts[o] = seed + v * 0.01f;
            verts[o + 1] = seed * 0.1f + v * 0.02f;
            verts[o + 2] = seed * 0.2f + v * 0.03f;
        }

        return verts;
    }

    private static uint[] CreatePreviewIndices(int indexCount)
    {
        var indices = new uint[indexCount];
        for (var i = 0; i < indexCount; i++)
        {
            indices[i] = (uint)(i % 192);
        }

        return indices;
    }
}
