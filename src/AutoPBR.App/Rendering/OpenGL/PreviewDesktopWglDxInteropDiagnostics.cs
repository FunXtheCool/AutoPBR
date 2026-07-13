using System.Runtime.InteropServices;

using Avalonia.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal enum PreviewDesktopWglDxInteropFailure
{
    None,
    ExtensionUnavailable,
    PresentationDeviceUnavailable,
    SharedExportUnavailable,
    OpenDeviceFailed,
    OpenSharedTextureFailed,
    RegisterTextureFailed,
    LockObjectFailed,
    FramebufferIncomplete,
    Exception,
}

internal static class PreviewDesktopWglDxInteropDiagnostics
{
    public static string Describe(PreviewDesktopWglDxInteropFailure failure, int detail = 0)
    {
        return failure switch
        {
            PreviewDesktopWglDxInteropFailure.None => "ok",
            PreviewDesktopWglDxInteropFailure.ExtensionUnavailable =>
                "WGL_NV_DX_interop entry points unavailable on the sidecar OpenGL context (often wrong GPU adapter or procs queried without a current WGL context).",
            PreviewDesktopWglDxInteropFailure.PresentationDeviceUnavailable =>
                "ANGLE D3D11 presentation device could not be resolved from Avalonia external-objects.",
            PreviewDesktopWglDxInteropFailure.SharedExportUnavailable =>
                "Shared D3D11 export texture could not be created on the ANGLE presentation context.",
            PreviewDesktopWglDxInteropFailure.OpenDeviceFailed =>
                $"wglDXOpenDeviceNV failed (win32=0x{detail:x}). The sidecar OpenGL adapter likely differs from ANGLE's D3D11 adapter (common on Intel iGPU + NVIDIA dGPU).",
            PreviewDesktopWglDxInteropFailure.OpenSharedTextureFailed =>
                $"OpenSharedResource failed (hr=0x{detail:x8}).",
            PreviewDesktopWglDxInteropFailure.RegisterTextureFailed =>
                $"wglDXRegisterObjectNV failed (win32=0x{detail:x}).",
            PreviewDesktopWglDxInteropFailure.LockObjectFailed =>
                $"wglDXLockObjectsNV failed (win32=0x{detail:x}).",
            PreviewDesktopWglDxInteropFailure.FramebufferIncomplete =>
                "DX-interop sidecar FBO was incomplete.",
            PreviewDesktopWglDxInteropFailure.Exception =>
                "D3D11/WGL interop presentation failed with an exception.",
            _ => failure.ToString(),
        };
    }

    public static bool IsPermanentFailure(PreviewDesktopWglDxInteropFailure failure) =>
        failure is PreviewDesktopWglDxInteropFailure.Exception
            or PreviewDesktopWglDxInteropFailure.ExtensionUnavailable
            or PreviewDesktopWglDxInteropFailure.OpenDeviceFailed
            or PreviewDesktopWglDxInteropFailure.OpenSharedTextureFailed
            or PreviewDesktopWglDxInteropFailure.RegisterTextureFailed
            // LockObjectFailed is often transient NV_DX contention — keep retrying.
            or PreviewDesktopWglDxInteropFailure.FramebufferIncomplete
            or PreviewDesktopWglDxInteropFailure.PresentationDeviceUnavailable
            or PreviewDesktopWglDxInteropFailure.SharedExportUnavailable;
}

internal static class PreviewWin32Error
{
    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    public static int GetLastErrorCode() => (int)GetLastError();
}
