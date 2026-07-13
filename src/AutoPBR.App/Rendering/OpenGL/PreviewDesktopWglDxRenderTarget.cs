using Avalonia.OpenGL;
using Avalonia.Platform;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Binds a shared D3D11 export texture into the desktop WGL sidecar via NV_DX_interop.</summary>
internal sealed class PreviewDesktopWglDxRenderTarget : IDisposable
{
    private IntPtr _dxInteropDevice;
    private IntPtr _registeredObject;
    private IntPtr _d3d11Texture2D;
    private uint _glTexture;
    private uint _fbo;
    private uint _depthRenderbuffer;
    private int _width;
    private int _height;
    private IntPtr _boundSharedHandle;
    private IntPtr _boundD3d11Device;
    private IDisposable? _sharedTextureLifetime;
    private PreviewDxgiKeyedMutex? _keyedMutex;
    private bool _abandoned;

    public bool IsActive => _fbo != 0;

    public int Framebuffer => (int)_fbo;

    public PreviewDxgiKeyedMutex? KeyedMutex => _keyedMutex;

    /// <summary>Opens/registers the shared texture without NV_DX lock (creates native keyed mutex).</summary>
    public bool TryEnsureRegistered(
        GL wglGl,
        GlInterface wglGlInterface,
        in PreviewAngleD3D11Presentation presentation,
        IPlatformHandle sharedHandle,
        out PreviewDesktopWglDxInteropFailure failure,
        out int detail)
    {
        failure = PreviewDesktopWglDxInteropFailure.None;
        detail = 0;
        if (_abandoned)
        {
            failure = PreviewDesktopWglDxInteropFailure.LockObjectFailed;
            detail = 0;
            return false;
        }

        return EnsureInteropRegistration(wglGl, wglGlInterface, presentation, sharedHandle, out failure, out detail);
    }

    public bool TryBegin(
        GL wglGl,
        GlInterface wglGlInterface,
        in PreviewAngleD3D11Presentation presentation,
        IPlatformHandle sharedHandle,
        int width,
        int height,
        out PreviewDesktopWglDxInteropFailure failure,
        out int detail)
    {
        failure = PreviewDesktopWglDxInteropFailure.None;
        detail = 0;
        if (_abandoned)
        {
            failure = PreviewDesktopWglDxInteropFailure.LockObjectFailed;
            detail = 0;
            return false;
        }

        if (!EnsureInteropRegistration(wglGl, wglGlInterface, presentation, sharedHandle, out failure, out detail))
        {
            return false;
        }

        if (!PreviewDesktopWglDxInterop.TryLockObject(_dxInteropDevice, _registeredObject))
        {
            failure = PreviewDesktopWglDxInteropFailure.LockObjectFailed;
            detail = PreviewWin32Error.GetLastErrorCode();
            return false;
        }

        EnsureFramebufferAttachments(wglGl, width, height);
        if (_fbo == 0)
        {
            End();
            failure = PreviewDesktopWglDxInteropFailure.FramebufferIncomplete;
            detail = 0;
            return false;
        }

        return true;
    }

    public void End()
    {
        if (_dxInteropDevice == IntPtr.Zero || _registeredObject == IntPtr.Zero)
        {
            return;
        }

        PreviewDesktopWglDxInterop.TryUnlockObject(_dxInteropDevice, _registeredObject);
    }

    public void Dispose()
    {
        TeardownInterop();
    }

    public void DestroyGlResources(GL wglGl)
    {
        TeardownInterop(wglGl);
    }

    /// <summary>
    /// Leaves a suspect NV_DX registration untouched for process cleanup. Calling
    /// Unlock/Unregister after a never-signaled GL fence can wedge the WGL owner thread.
    /// </summary>
    public void AbandonInteropResources()
    {
        _abandoned = true;
        _registeredObject = IntPtr.Zero;
        _dxInteropDevice = IntPtr.Zero;
        _d3d11Texture2D = IntPtr.Zero;
        _keyedMutex = null;
        _sharedTextureLifetime = null;
        _boundSharedHandle = IntPtr.Zero;
        _boundD3d11Device = IntPtr.Zero;
        _fbo = 0;
        _depthRenderbuffer = 0;
        _glTexture = 0;
        _width = 0;
        _height = 0;
    }

    private bool EnsureInteropRegistration(
        GL wglGl,
        GlInterface wglGlInterface,
        in PreviewAngleD3D11Presentation presentation,
        IPlatformHandle sharedHandle,
        out PreviewDesktopWglDxInteropFailure failure,
        out int detail)
    {
        failure = PreviewDesktopWglDxInteropFailure.None;
        detail = 0;
        if (_registeredObject != IntPtr.Zero &&
            _boundSharedHandle == sharedHandle.Handle &&
            _boundD3d11Device == presentation.NativeDevice)
        {
            return true;
        }

        TeardownInterop(wglGl);

        if (!PreviewDesktopWglDxInterop.EnsureProcs(wglGlInterface))
        {
            failure = PreviewDesktopWglDxInteropFailure.ExtensionUnavailable;
            detail = PreviewWin32Error.GetLastErrorCode();
            return false;
        }

        if (!PreviewDesktopWglDxInterop.TryOpenDevice(wglGlInterface, presentation.NativeDevice, out _dxInteropDevice))
        {
            failure = PreviewDesktopWglDxInteropFailure.OpenDeviceFailed;
            detail = PreviewWin32Error.GetLastErrorCode();
            return false;
        }

        if (!PreviewAngleD3D11Access.TryOpenSharedTexture2D(
                presentation,
                sharedHandle,
                out _d3d11Texture2D,
                out _sharedTextureLifetime,
                out var hr))
        {
            failure = PreviewDesktopWglDxInteropFailure.OpenSharedTextureFailed;
            detail = hr;
            PreviewDesktopWglDxInterop.CloseDevice(_dxInteropDevice);
            _dxInteropDevice = IntPtr.Zero;
            return false;
        }

        _glTexture = wglGl.GenTexture();
        if (!PreviewDesktopWglDxInterop.TryRegisterTexture2D(_dxInteropDevice, _d3d11Texture2D, _glTexture, out _registeredObject))
        {
            failure = PreviewDesktopWglDxInteropFailure.RegisterTextureFailed;
            detail = PreviewWin32Error.GetLastErrorCode();
            CleanupPartial(wglGl);
            return false;
        }

        if (!PreviewDxgiKeyedMutex.TryCreateFromTexture2D(_d3d11Texture2D, out _keyedMutex, out var mutexHr) ||
            _keyedMutex is null)
        {
            failure = PreviewDesktopWglDxInteropFailure.OpenSharedTextureFailed;
            detail = mutexHr;
            CleanupPartial(wglGl);
            return false;
        }

        _boundSharedHandle = sharedHandle.Handle;
        _boundD3d11Device = presentation.NativeDevice;
        return true;
    }

    private void EnsureFramebufferAttachments(GL wglGl, int width, int height)
    {
        if (_fbo != 0 && _width == width && _height == height)
        {
            wglGl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            return;
        }

        if (_fbo != 0)
        {
            wglGl.DeleteFramebuffer(_fbo);
            _fbo = 0;
        }

        if (_depthRenderbuffer != 0)
        {
            wglGl.DeleteRenderbuffer(_depthRenderbuffer);
            _depthRenderbuffer = 0;
        }

        _width = width;
        _height = height;

        _depthRenderbuffer = wglGl.GenRenderbuffer();
        wglGl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRenderbuffer);
        wglGl.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24,
            (uint)width,
            (uint)height);

        _fbo = wglGl.GenFramebuffer();
        wglGl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        wglGl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _glTexture,
            0);
        wglGl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer);

        if (wglGl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            wglGl.DeleteFramebuffer(_fbo);
            wglGl.DeleteRenderbuffer(_depthRenderbuffer);
            _fbo = 0;
            _depthRenderbuffer = 0;
            _width = 0;
            _height = 0;
        }
    }

    private void TeardownInterop(GL? wglGl = null)
    {
        if (_abandoned)
        {
            return;
        }

        End();

        if (_registeredObject != IntPtr.Zero && _dxInteropDevice != IntPtr.Zero)
        {
            PreviewDesktopWglDxInterop.UnregisterObject(_dxInteropDevice, _registeredObject);
            _registeredObject = IntPtr.Zero;
        }

        if (wglGl is not null && _fbo != 0)
        {
            wglGl.DeleteFramebuffer(_fbo);
            _fbo = 0;
        }

        if (wglGl is not null && _depthRenderbuffer != 0)
        {
            wglGl.DeleteRenderbuffer(_depthRenderbuffer);
            _depthRenderbuffer = 0;
        }

        if (wglGl is not null && _glTexture != 0)
        {
            wglGl.DeleteTexture(_glTexture);
            _glTexture = 0;
        }

        if (_dxInteropDevice != IntPtr.Zero)
        {
            PreviewDesktopWglDxInterop.CloseDevice(_dxInteropDevice);
            _dxInteropDevice = IntPtr.Zero;
        }

        _d3d11Texture2D = IntPtr.Zero;
        _keyedMutex?.Dispose();
        _keyedMutex = null;
        _sharedTextureLifetime?.Dispose();
        _sharedTextureLifetime = null;
        _boundSharedHandle = IntPtr.Zero;
        _boundD3d11Device = IntPtr.Zero;
        _width = 0;
        _height = 0;
    }

    private void CleanupPartial(GL wglGl)
    {
        if (_registeredObject != IntPtr.Zero && _dxInteropDevice != IntPtr.Zero)
        {
            PreviewDesktopWglDxInterop.UnregisterObject(_dxInteropDevice, _registeredObject);
            _registeredObject = IntPtr.Zero;
        }

        if (_glTexture != 0)
        {
            wglGl.DeleteTexture(_glTexture);
            _glTexture = 0;
        }

        if (_dxInteropDevice != IntPtr.Zero)
        {
            PreviewDesktopWglDxInterop.CloseDevice(_dxInteropDevice);
            _dxInteropDevice = IntPtr.Zero;
        }

        _d3d11Texture2D = IntPtr.Zero;
        _keyedMutex?.Dispose();
        _keyedMutex = null;
        _sharedTextureLifetime?.Dispose();
        _sharedTextureLifetime = null;
        _boundSharedHandle = IntPtr.Zero;
        _boundD3d11Device = IntPtr.Zero;
    }
}
