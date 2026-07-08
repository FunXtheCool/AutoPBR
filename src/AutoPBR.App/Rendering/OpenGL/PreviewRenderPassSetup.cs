using AutoPBR.App.Rendering.Scene;
using AutoPBR.Preview;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Entity emulated idle animation clock for OpenGL pass setup (extracted from pass setup partial).</summary>
internal static class PreviewRenderPassSetup
{
    internal static bool IsParityCatalogCpuBindReady(
        bool setupAnimMotion,
        PreviewModelSubject blockModel,
        bool bindPoseCommitted) =>
        !setupAnimMotion &&
        bindPoseCommitted &&
        !blockModel.GpuEntityBoneSkinning &&
        blockModel.EntityPreviewPlacementApplied &&
        blockModel.InterleavedVertices.Length > 0 &&
        blockModel.Indices.Length > 0;

    internal static float ResolveEntityEmulatedAnimClock(
        in GlRenderFrame frame,
        ref bool prevPauseEntityIdleAnimation,
        ref float frozenEntityIdleAnimClock,
        out bool pauseEdge)
    {
        pauseEdge = false;
        if (!frame.EntityEmulatedPreview || frame.BlockModel is null || frame.EntityRebakeCtx is null)
        {
            return 0f;
        }

        var speed = Math.Clamp(frame.Settings.EntityAnimationSpeed, 0f, 4f);
        var amp = Math.Clamp(frame.Settings.EntityAnimationAmplitude, 0f, 2f);
        var paused = frame.Settings.PauseEntityIdleAnimation;
        float clock;
        if (paused)
        {
            if (!prevPauseEntityIdleAnimation)
            {
                frozenEntityIdleAnimClock = (float)(frame.RenderTime * speed * amp);
            }

            clock = frozenEntityIdleAnimClock;
        }
        else
        {
            clock = (float)(frame.RenderTime * speed * amp);
        }

        pauseEdge = paused != prevPauseEntityIdleAnimation;
        prevPauseEntityIdleAnimation = paused;
        return clock;
    }
}
