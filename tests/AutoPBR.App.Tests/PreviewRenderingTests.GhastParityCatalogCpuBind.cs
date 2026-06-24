using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    private const string GhastTexturePath = "assets/minecraft/textures/entity/ghast/ghast.png";
    private const string HappyGhastTexturePath = "assets/minecraft/textures/entity/ghast/happy_ghast.png";

    [Theory]
    [InlineData(GhastTexturePath)]
    [InlineData(HappyGhastTexturePath)]
    public void SetBlockModelPreview_drops_stale_ghast_parity_cpu_when_pack_fingerprint_differs_from_committed_mesh(
        string texturePath)
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateGhastTextureMaps(texturePath);
        var slotMaterials = CreateGhastSlotMaterials(textureMaps);

        var committedVerts = CreatePreviewVerts(240, seed: 11);
        var packVerts = CreatePreviewVerts(240, seed: 12);
        var indices = CreatePreviewIndices(360);
        var packFingerprint = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
            packVerts,
            PreviewMesh.FloatsPerVertex);

        var committedSubject = CreateGhastSubject(
            committedVerts,
            indices,
            textureMaps,
            CreateGhastRebakeContext(texturePath, packFingerprint: packFingerprint),
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        var packSubject = CreateGhastSubject(
            packVerts,
            indices,
            textureMaps,
            CreateGhastRebakeContext(texturePath, packFingerprint: packFingerprint),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(packVerts, stored!.InterleavedVertices);
        Assert.Null(backend.TestEntityBindPoseCommittedKey);
        Assert.True(backend.TestMeshDirty);
    }

    [Theory]
    [InlineData(GhastTexturePath)]
    [InlineData(HappyGhastTexturePath)]
    public void SetBlockModelPreview_keeps_committed_ghast_parity_cpu_subject_on_ui_repush(string texturePath)
    {
        var backend = new OpenGlPreviewBackend();
        var diagnostics = new List<string>();
        backend.SetDiagnosticLog(diagnostics.Add);

        var textureMaps = CreateGhastTextureMaps(texturePath);
        var slotMaterials = CreateGhastSlotMaterials(textureMaps);

        var committedVerts = CreatePreviewVerts(240, seed: 11);
        var packVerts = CreatePreviewVerts(240, seed: 12);
        var indices = CreatePreviewIndices(360);
        var committedFingerprint = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
            committedVerts,
            PreviewMesh.FloatsPerVertex);
        var rebake = CreateGhastRebakeContext(texturePath, packFingerprint: committedFingerprint);

        var committedSubject = CreateGhastSubject(
            committedVerts,
            indices,
            textureMaps,
            rebake,
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        var packSubject = CreateGhastSubject(
            packVerts,
            indices,
            textureMaps,
            CreateGhastRebakeContext(texturePath, packFingerprint: committedFingerprint),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(committedVerts, stored!.InterleavedVertices);
        Assert.True(stored.EntityGpuVerticesInPreviewSpace);
        Assert.False(backend.TestMeshDirty);
        Assert.NotNull(backend.TestEntityBindPoseCommittedKey);
        Assert.True(EntityTextureParityCatalog.IsCatalogued(texturePath));

        Assert.DoesNotContain(
            diagnostics,
            m => m.Contains("Fallback mesh upload used", StringComparison.Ordinal));
        Assert.DoesNotContain(
            diagnostics,
            m => m.Contains("Mesh upload: pack-converter CPU subject", StringComparison.Ordinal));
        Assert.DoesNotContain(
            diagnostics,
            m => m.Contains("Entity mesh upload deferred", StringComparison.Ordinal));
    }

    private static PreviewTextureMaps[] CreateGhastTextureMaps(string texturePath)
    {
        var (w, h) = texturePath.Contains("happy_ghast", StringComparison.Ordinal) ? (64, 64) : (64, 32);
        return
        [
            new()
            {
                Width = w,
                Height = h,
                DiffuseRgba = new byte[w * h * 4],
                NormalRgba = new byte[w * h * 4],
                SpecularRgba = new byte[w * h * 4],
                HeightRgba = new byte[w * h * 4],
            }
        ];
    }

    private static PreviewMaterial[] CreateGhastSlotMaterials(PreviewTextureMaps[] textureMaps) =>
        textureMaps.Select(PreviewMaterialMapper.FromCoreMaps).ToArray();

    private static EntityEmulatedPreviewRebakeContext CreateGhastRebakeContext(string texturePath, ulong packFingerprint) =>
        new()
        {
            PackZipPath = "minecraft-26.1.2-client.jar",
            AssetArchivePath = texturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.3f,
            OrderedTextureZipPaths = [texturePath],
            PackConverterCpuMeshFingerprint = packFingerprint,
        };

    private static PreviewModelSubject CreateGhastSubject(
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
}
