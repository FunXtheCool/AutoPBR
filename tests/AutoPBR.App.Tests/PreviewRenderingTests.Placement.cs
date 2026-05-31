using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Fact]
    public void SubjectPlacementUnitCubeDoesNotNeedLiftForCurrentStageFloor()
    {
        // Block/item preview uses a centered ±0.5 cube; the grid is slightly lower (see PreviewStageConstants).
        // This documents the real staging relationship — not every preview mesh dips through the floor.
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(
            mesh.InterleavedVertices,
            clearance: 0.002f);
        Assert.Equal(0f, lift);
    }

    [Fact]
    public void SubjectPlacementComputesPositiveLiftWhenLowestVertexIsBelowFloor()
    {
        // Unit test for PreviewSubjectPlacement math only: any baked mesh (entity rigs included) whose minimum Y
        // falls below floor+clearance gets a compensating lift. Horse/pig/chicken parity belongs in Core mesh tests;
        // here we only need one vertex row with Y through the floor plane.
        var floor = PreviewStageConstants.GridWorldY;
        var verts = new float[PreviewMesh.FloatsPerVertex];
        verts[1] = floor - 0.25f;

        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(verts, floor, clearance: 0.002f);
        Assert.True(lift > 0f);
        Assert.Equal(floor + 0.002f - verts[1], lift, precision: 5);
    }

    [Fact]
    public void SubjectPlacementLiftedMeshMinYStaysAtOrAboveGridClearance()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(
            mesh.InterleavedVertices,
            clearance: 0.002f);
        var lifted = PreviewSubjectPlacement.ApplyLift(mesh.InterleavedVertices, lift);
        var minY = float.PositiveInfinity;
        for (var i = 1; i < lifted.Length; i += PreviewMesh.FloatsPerVertex)
        {
            minY = MathF.Min(minY, lifted[i]);
        }

        Assert.True(minY >= PreviewStageConstants.GridWorldY + 0.0019f);
    }

    [Fact]
    public void SubjectPlacementLiftPreservesEmulatedEntityRebakeAndGpuSkinningFlags()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var verts = (float[])mesh.InterleavedVertices.Clone();
        const int s = PreviewMesh.FloatsPerVertex;
        for (var i = 1; i < verts.Length; i += s)
        {
            verts[i] -= 2f;
        }

        var mats = new PreviewTextureMaps[]
        {
            new()
            {
                Width = 1,
                Height = 1,
                DiffuseRgba = new byte[4]
            }
        };

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = "assets/minecraft/textures/entity/horse/horse_white.png",
            NativeRootDirectory = Path.GetTempPath(),
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.2f,
            OrderedTextureZipPaths = ["assets/minecraft/textures/entity/horse/horse_white.png"]
        };

        var subject = new PreviewModelSubject
        {
            InterleavedVertices = verts,
            Indices = mesh.Indices,
            DrawBatches = [new PreviewDrawBatch(0, mesh.Indices.Length, 0)],
            Materials = mats,
            AnimationPreset = "entity_emulated",
            EmulatedRebake = rebake,
            GpuEntityBoneSkinning = true,
            VertexStrideFloats = 12,
            EntityGpuMeshSpaceLiftY = 0.01f
        };

        var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(subject);
        Assert.Same(subject, lifted);
        Assert.NotNull(lifted.EmulatedRebake);
        Assert.Same(rebake, lifted.EmulatedRebake);
        Assert.True(lifted.GpuEntityBoneSkinning);
        Assert.Equal(12, lifted.VertexStrideFloats);
        Assert.Equal(0.01f, lifted.EntityGpuMeshSpaceLiftY);
    }
}
