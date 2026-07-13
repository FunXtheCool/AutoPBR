using System.Runtime.InteropServices;

using Avalonia.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Native WGL presentation helpers for desktop OpenGL preview contexts.</summary>
internal static class PreviewWglPresentation
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglSwapIntervalExtDelegate(int interval);

    /// <summary>Sets swap interval via <c>wglSwapIntervalEXT</c>. 0 = vsync off (uncapped), 1 = vsync on.</summary>
    public static bool TrySetSwapInterval(GlInterface gl, int interval)
    {
        var proc = gl.GetProcAddress("wglSwapIntervalEXT");
        if (proc == IntPtr.Zero)
        {
            return false;
        }

        var fn = Marshal.GetDelegateForFunctionPointer<WglSwapIntervalExtDelegate>(proc);
        return fn(interval);
    }
}
