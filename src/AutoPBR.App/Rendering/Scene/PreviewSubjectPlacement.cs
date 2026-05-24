using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Rendering.Scene;

/// <summary>
/// Keeps preview subjects from clipping through the grid/ground stage by lifting meshes so their lowest vertex
/// stays at or above a configurable floor height.
/// </summary>
public static class PreviewSubjectPlacement
{
    public static float ComputeLiftToAvoidGroundClip(
        ReadOnlySpan<float> interleavedVertices,
        float floorY = PreviewStageConstants.GridWorldY,
        float clearance = 0.002f,
        int vertexStrideFloats = PreviewMesh.FloatsPerVertex)
    {
        if (vertexStrideFloats < 2 ||
            interleavedVertices.Length < vertexStrideFloats ||
            interleavedVertices.Length % vertexStrideFloats != 0)
        {
            return 0f;
        }

        var minY = float.PositiveInfinity;
        for (var i = 1; i < interleavedVertices.Length; i += vertexStrideFloats)
        {
            minY = MathF.Min(minY, interleavedVertices[i]);
        }

        if (!float.IsFinite(minY))
        {
            return 0f;
        }

        var targetMinY = floorY + MathF.Max(0f, clearance);
        return MathF.Max(0f, targetMinY - minY);
    }

    public static float[] ApplyLift(float[] interleavedVertices, float liftY, int vertexStrideFloats = PreviewMesh.FloatsPerVertex)
    {
        if (liftY <= 0f || interleavedVertices.Length == 0)
        {
            return interleavedVertices;
        }

        if (vertexStrideFloats < 2 || interleavedVertices.Length % vertexStrideFloats != 0)
        {
            return interleavedVertices;
        }

        var lifted = (float[])interleavedVertices.Clone();
        for (var i = 1; i < lifted.Length; i += vertexStrideFloats)
        {
            lifted[i] += liftY;
        }

        return lifted;
    }

    public static PreviewModelSubject LiftSubjectIfClipping(
        PreviewModelSubject subject,
        float floorY = PreviewStageConstants.GridWorldY,
        float clearance = 0.002f)
    {
        var stride = subject.VertexStrideFloats > 0 ? subject.VertexStrideFloats : PreviewMesh.FloatsPerVertex;
        var liftY = ComputeLiftToAvoidGroundClip(subject.InterleavedVertices, floorY, clearance, stride);
        if (liftY <= 0f)
        {
            return subject;
        }

        return new PreviewModelSubject
        {
            InterleavedVertices = ApplyLift(subject.InterleavedVertices, liftY, stride),
            Indices = subject.Indices,
            DrawBatches = subject.DrawBatches,
            Materials = subject.Materials,
            PrimaryMaterialIndex = subject.PrimaryMaterialIndex,
            Sprite2DFoliageTarget = subject.Sprite2DFoliageTarget,
            EnableRenderTimeAnimation = subject.EnableRenderTimeAnimation,
            AnimationPreset = subject.AnimationPreset,
            EmulatedRebake = subject.EmulatedRebake,
            GpuEntityBoneSkinning = subject.GpuEntityBoneSkinning,
            VertexStrideFloats = subject.VertexStrideFloats,
            EntityGpuMeshSpaceLiftY = subject.EntityGpuMeshSpaceLiftY
        };
    }
}
