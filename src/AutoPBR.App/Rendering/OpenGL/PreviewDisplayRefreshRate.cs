using System.Runtime.InteropServices;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class PreviewDisplayRefreshRate
{
    private const int VRefresh = 116;

    public static int? TryGetForWindow(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
        {
            return null;
        }

        var dc = GetDC(hwnd);
        if (dc == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var hz = GetDeviceCaps(dc, VRefresh);
            return hz > 1 && hz < 1000 ? hz : null;
        }
        finally
        {
            _ = ReleaseDC(hwnd, dc);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int index);
}
