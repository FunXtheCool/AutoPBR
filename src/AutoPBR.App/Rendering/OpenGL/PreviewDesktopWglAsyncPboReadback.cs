using System.Buffers;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Double-buffered async <c>GL_PIXEL_PACK_BUFFER</c> readback with fence sync and pooled CPU staging.
/// Removes per-frame <c>byte[]</c> allocation and avoids <c>glFinish</c> on the WGL sidecar path.
/// </summary>
internal sealed class PreviewDesktopWglAsyncPboReadback : IDisposable
{
    private const GLEnum PixelPackBuffer = (GLEnum)0x88EB;

    private readonly GL _gl;
    private readonly uint[] _pbos = new uint[2];
    private readonly nint[] _fences = new nint[2];
    private int _readIndex;
    private int _framesSubmitted;
    private int _width;
    private int _height;
    private int _byteCount;
    private byte[]? _staging;
    private bool _stagingValid;
    private bool _loggedReady;

    public PreviewDesktopWglAsyncPboReadback(GL gl)
    {
        _gl = gl;
    }

    public bool UsesAsyncPath => _loggedReady;

    public bool TryCollect(
        uint readFbo,
        int width,
        int height,
        out ReadOnlySpan<byte> pixels,
        bool forceSyncPresent = false)
    {
        pixels = default;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var byteCount = checked(width * height * 4);
        if (!EnsureBuffers(width, height, byteCount))
        {
            return false;
        }

        var consumed = false;
        if (forceSyncPresent)
        {
            consumed = TryBootstrapSyncRead(readFbo, width, height, byteCount);
        }
        else if (_framesSubmitted > 0)
        {
            consumed = TryConsumeReadyPbo(byteCount);
        }
        else
        {
            consumed = TryBootstrapSyncRead(readFbo, width, height, byteCount);
        }

        IssueAsyncRead(readFbo, width, height, _readIndex);
        _readIndex = 1 - _readIndex;
        _framesSubmitted++;

        if (consumed)
        {
            _stagingValid = true;
            if (!_loggedReady)
            {
                _loggedReady = true;
            }

            pixels = _staging!.AsSpan(0, byteCount);
            return true;
        }

        if (_stagingValid)
        {
            pixels = _staging!.AsSpan(0, byteCount);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        for (var i = 0; i < _pbos.Length; i++)
        {
            DeleteFence(i);
            if (_pbos[i] != 0)
            {
                _gl.DeleteBuffer(_pbos[i]);
                _pbos[i] = 0;
            }
        }

        ReturnStaging();
        _framesSubmitted = 0;
        _readIndex = 0;
        _stagingValid = false;
        _loggedReady = false;
        _width = 0;
        _height = 0;
        _byteCount = 0;
    }

    private bool EnsureBuffers(int width, int height, int byteCount)
    {
        if (_pbos[0] != 0 && _width == width && _height == height && _byteCount == byteCount)
        {
            return true;
        }

        ResetGpuBuffers();
        _width = width;
        _height = height;
        _byteCount = byteCount;
        _framesSubmitted = 0;
        _readIndex = 0;
        _stagingValid = false;

        for (var i = 0; i < _pbos.Length; i++)
        {
            _pbos[i] = _gl.GenBuffer();
            _gl.BindBuffer(PixelPackBuffer, _pbos[i]);
            unsafe
            {
                _gl.BufferData(PixelPackBuffer, (nuint)byteCount, null, BufferUsageARB.StreamRead);
            }
        }

        _gl.BindBuffer(PixelPackBuffer, 0);
        return _pbos[0] != 0 && _pbos[1] != 0;
    }

    private void ResetGpuBuffers()
    {
        for (var i = 0; i < _pbos.Length; i++)
        {
            DeleteFence(i);
            if (_pbos[i] != 0)
            {
                _gl.DeleteBuffer(_pbos[i]);
                _pbos[i] = 0;
            }
        }
    }

    private bool TryBootstrapSyncRead(uint readFbo, int width, int height, int byteCount)
    {
        EnsureStaging(byteCount);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, readFbo);
        unsafe
        {
            fixed (byte* staging = _staging)
            {
                _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, staging);
            }
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return true;
    }

    private void IssueAsyncRead(uint readFbo, int width, int height, int pboIndex)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, readFbo);
        _gl.BindBuffer(PixelPackBuffer, _pbos[pboIndex]);
        unsafe
        {
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        ReplaceFence(pboIndex);
        _gl.BindBuffer(PixelPackBuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private bool TryConsumeReadyPbo(int byteCount)
    {
        var pboIndex = 1 - _readIndex;
        if (!IsFenceReady(pboIndex))
        {
            return false;
        }

        EnsureStaging(byteCount);
        _gl.BindBuffer(PixelPackBuffer, _pbos[pboIndex]);
        if (!TryCopyMappedPbo(byteCount))
        {
            _gl.BindBuffer(PixelPackBuffer, 0);
            return false;
        }

        DeleteFence(pboIndex);
        return true;
    }

    private bool TryCopyMappedPbo(int byteCount)
    {
        unsafe
        {
            var ptr = _gl.MapBufferRange(
                PixelPackBuffer,
                0,
                (uint)byteCount,
                (uint)GLEnum.MapReadBit);
            if (ptr == null)
            {
                return false;
            }

            new ReadOnlySpan<byte>(ptr, byteCount).CopyTo(_staging!.AsSpan(0, byteCount));
            _gl.UnmapBuffer(PixelPackBuffer);
            _gl.BindBuffer(PixelPackBuffer, 0);
            return true;
        }
    }

    private void ReplaceFence(int pboIndex)
    {
        DeleteFence(pboIndex);
        _fences[pboIndex] = _gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, (uint)0);
    }

    private bool IsFenceReady(int pboIndex)
    {
        var fence = _fences[pboIndex];
        if (fence == 0)
        {
            return _framesSubmitted > 1;
        }

        var status = _gl.ClientWaitSync(fence, (uint)0, 0);
        return IsFenceWaitComplete(status);
    }

    private static bool IsFenceWaitComplete(GLEnum status)
    {
        return status is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied ||
               (int)status is 0x911A or 0x911B;
    }

    private void DeleteFence(int pboIndex)
    {
        if (_fences[pboIndex] == 0)
        {
            return;
        }

        _gl.DeleteSync(_fences[pboIndex]);
        _fences[pboIndex] = 0;
    }

    private void EnsureStaging(int byteCount)
    {
        if (_staging is not null && _staging.Length >= byteCount)
        {
            return;
        }

        ReturnStaging();
        _staging = ArrayPool<byte>.Shared.Rent(byteCount);
    }

    private void ReturnStaging()
    {
        if (_staging is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_staging);
        _staging = null;
    }
}
