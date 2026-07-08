using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png", 10557795273113060837UL)]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png", 294592320418992677UL)]
    [InlineData("assets/minecraft/textures/entity/camel/camel_baby.png", 17390841223401462245UL)]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png", 4052047643320367269UL)]
    public void TryRebakeMesh_baby_textures_match_core_uv_golden(string texturePath, ulong goldenUvFingerprint)
    {
        var materials = CreateBabyTextureMaps([texturePath], provenance: null);
        var rebake = CreateBabyRebakeContext(texturePath, [texturePath]);

        Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
            rebake,
            materials,
            animationTimeSeconds: 0f,
            out var rebakedVerts,
            out _,
            out _,
            applyGeometryIrSetupAnimMotion: false));

        var uvFp = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMeshUvFingerprint(
            rebakedVerts!,
            PreviewMesh.FloatsPerVertex);
        Assert.Equal(goldenUvFingerprint, uvFp);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png")]
    [InlineData("assets/minecraft/textures/entity/camel/camel_baby.png")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png")]
    public void SetBlockModelPreview_drops_stale_baby_parity_cpu_when_rebake_fingerprint_differs(string texturePath)
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateBabyTextureMaps([texturePath], null);
        var slotMaterials = textureMaps.Select(m => PreviewMaterialMapper.FromCoreMaps(m)).ToArray();

        var staleVerts = CreatePreviewVerts(240, seed: 41);
        var freshVerts = CreatePreviewVerts(240, seed: 42);
        var indices = CreatePreviewIndices(360);
        var staleFingerprint = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
            staleVerts,
            PreviewMesh.FloatsPerVertex);
        var freshFingerprint = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
            freshVerts,
            PreviewMesh.FloatsPerVertex);

        var committedSubject = CreateBabySubject(
            staleVerts,
            indices,
            textureMaps,
            CreateBabyRebakeContext(texturePath, [texturePath], staleFingerprint),
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        var packSubject = CreateBabySubject(
            freshVerts,
            indices,
            textureMaps,
            CreateBabyRebakeContext(texturePath, [texturePath], freshFingerprint),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(freshVerts, stored!.InterleavedVertices);
        Assert.Null(backend.TestEntityBindPoseCommittedKey);
        Assert.True(backend.TestMeshDirty);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_temperate_baby.png")]
    [InlineData("assets/minecraft/textures/entity/camel/camel_baby.png")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png")]
    public void SetBlockModelPreview_keeps_committed_baby_parity_cpu_subject_on_ui_repush(string texturePath)
    {
        var backend = new OpenGlPreviewBackend();
        var textureMaps = CreateBabyTextureMaps([texturePath], null);
        var slotMaterials = textureMaps.Select(m => PreviewMaterialMapper.FromCoreMaps(m)).ToArray();

        var committedVerts = CreatePreviewVerts(240, seed: 11);
        var packVerts = CreatePreviewVerts(240, seed: 12);
        var indices = CreatePreviewIndices(360);
        var committedFingerprint = PreviewMeshGeometryFingerprint.ComputeCpuPreviewMesh(
            committedVerts,
            PreviewMesh.FloatsPerVertex);
        var rebake = CreateBabyRebakeContext(texturePath, [texturePath], committedFingerprint);

        var committedSubject = CreateBabySubject(
            committedVerts,
            indices,
            textureMaps,
            rebake,
            entityGpuVerticesInPreviewSpace: true,
            entityPreviewPlacementApplied: true);

        backend.TestSimulateParityCatalogCpuBindCommit(committedSubject);

        var packSubject = CreateBabySubject(
            packVerts,
            indices,
            textureMaps,
            CreateBabyRebakeContext(texturePath, [texturePath], committedFingerprint),
            entityGpuVerticesInPreviewSpace: false,
            entityPreviewPlacementApplied: false);

        backend.SetBlockModelPreview(packSubject, slotMaterials);

        var stored = backend.TestBlockModelSubject;
        Assert.NotNull(stored);
        Assert.Same(committedVerts, stored!.InterleavedVertices);
        Assert.True(stored.EntityGpuVerticesInPreviewSpace);
        Assert.True(stored.EntityPreviewPlacementApplied);
        Assert.False(backend.TestMeshDirty);
        Assert.NotNull(backend.TestEntityBindPoseCommittedKey);
        Assert.Equal(
            OpenGlPreviewBackend.TestBuildParityCatalogCpuBindCommitKey(rebake),
            backend.TestEntityBindPoseCommittedKey);
        Assert.True(EntityTextureParityCatalog.IsCatalogued(texturePath));
    }

    private static PreviewTextureMaps[] CreateBabyTextureMaps(
        IReadOnlyList<string> orderedPaths,
        PreviewMeshProvenance? provenance)
    {
        _ = provenance;
        var maps = new PreviewTextureMaps[orderedPaths.Count];
        for (var i = 0; i < orderedPaths.Count; i++)
        {
            var path = orderedPaths[i];
            var stem = Path.GetFileNameWithoutExtension(path);
            var rule = EntityTextureParityCatalog.ResolveRule(path, stem);
            var w = rule?.GeometryIrTextureWidth is > 0 and var rw ? rw : 64;
            var h = rule?.GeometryIrTextureHeight is > 0 and var rh ? rh : 64;
            maps[i] = new PreviewTextureMaps
            {
                Width = w,
                Height = h,
                DiffuseRgba = new byte[w * h * 4],
                NormalRgba = new byte[w * h * 4],
                SpecularRgba = new byte[w * h * 4],
                HeightRgba = new byte[w * h * 4],
            };
        }

        return maps;
    }

    private static EntityEmulatedPreviewRebakeContext CreateBabyRebakeContext(
        string texturePath,
        IReadOnlyList<string> orderedPaths,
        ulong packFingerprint = 0) =>
        new()
        {
            PackZipPath = "minecraft-26.1.2-client.jar",
            AssetArchivePath = texturePath,
            NativeRootDirectory = AppContext.BaseDirectory,
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0f,
            OrderedTextureZipPaths = orderedPaths.ToArray(),
            PackConverterCpuMeshFingerprint = packFingerprint,
        };

    private static PreviewModelSubject CreateBabySubject(
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
