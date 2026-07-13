using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Bounded GPU drain for DXGI/NV_DX handoff. Unbounded <c>glFinish</c> can wedge the
/// owner/present thread when the peer GPU still holds the shared resource.
/// </summary>
internal static class PreviewGlCommandDrain
{
    /// <summary>Default 8ms — enough for a blit without risking a multi-second hang.</summary>
    public const ulong DefaultTimeoutNanoseconds = 8_000_000UL;

    public static bool Drain(GL gl, ulong timeoutNanoseconds = DefaultTimeoutNanoseconds)
    {
        return TryDrain(gl, out _, timeoutNanoseconds);
    }

    public static bool TryDrain(GL gl, out nint pendingFence, ulong timeoutNanoseconds = DefaultTimeoutNanoseconds)
    {
        pendingFence = 0;
        // Flush so the fence is in the submitted stream, then wait with a hard cap.
        gl.Flush();
        var fence = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
        if (fence == 0)
        {
            return true;
        }

        var complete = IsFenceWaitComplete(gl.ClientWaitSync(fence, SyncObjectMask.Bit, timeoutNanoseconds));
        if (complete)
        {
            gl.DeleteSync(fence);
            return true;
        }

        pendingFence = fence;
        return false;
    }

    public static bool IsFenceReady(GL gl, nint fence)
    {
        if (fence == 0)
        {
            return true;
        }

        return IsFenceWaitComplete(gl.ClientWaitSync(fence, (uint)0, 0));
    }

    public static void DeleteFence(GL gl, nint fence)
    {
        if (fence != 0)
        {
            gl.DeleteSync(fence);
        }
    }

    private static bool IsFenceWaitComplete(GLEnum status) =>
        status is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied ||
        (int)status is 0x911A or 0x911B;
}
