using AutoPBR.Core.Models;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    /// <summary>Test-only: simulate a successful parity-catalog CPU bind-pose commit after GL rebake.</summary>
    internal void TestSimulateParityCatalogCpuBindCommit(PreviewModelSubject committedSubject)
    {
        lock (_sync)
        {
            _blockModelSubject = committedSubject;
            if (committedSubject.EmulatedRebake is { } ctx)
            {
                _entityBindPoseCommittedKey = PreviewRenderPassSetup.BuildParityCatalogCpuBindCommitKey(ctx);
            }

            _meshDirty = false;
        }
    }

    internal bool TestMeshDirty
    {
        get
        {
            lock (_sync)
            {
                return _meshDirty;
            }
        }
    }

    internal string? TestEntityBindPoseCommittedKey
    {
        get
        {
            lock (_sync)
            {
                return _entityBindPoseCommittedKey;
            }
        }
    }

    internal PreviewModelSubject? TestBlockModelSubject
    {
        get
        {
            lock (_sync)
            {
                return _blockModelSubject;
            }
        }
    }

    internal static string TestBuildParityCatalogCpuBindCommitKey(EntityEmulatedPreviewRebakeContext ctx) =>
        PreviewRenderPassSetup.BuildParityCatalogCpuBindCommitKey(ctx);

    internal static IReadOnlyDictionary<string, int> TestBuildGenesisProgramDefines(
        bool entitySkinningSsbo,
        bool materialDrawRecordSsbo,
        bool drawRecordBaseInstance = false,
        bool materialTextureArrays = false) =>
        BuildGenesisProgramDefines(
            GenesisShaderFeatureMask.None,
            entitySkinningSsbo,
            materialDrawRecordSsbo,
            materialTextureArrays,
            drawRecordBaseInstance);

    internal static bool TestTryResolveEntitySkinningDrawState(
        PreviewModelSubject? model,
        float meshSpaceLiftY,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool setupAnimMotion,
        out float previewSpaceVerts,
        out float bindMesh,
        out int gpuSkinning,
        out int boneCount,
        out float liftY) =>
        TryResolveEntitySkinningDrawState(
            model,
            meshSpaceLiftY,
            boneSnapshotValid,
            boneSnapshotCount,
            setupAnimMotion,
            out previewSpaceVerts,
            out bindMesh,
            out gpuSkinning,
            out boneCount,
            out liftY);
}
