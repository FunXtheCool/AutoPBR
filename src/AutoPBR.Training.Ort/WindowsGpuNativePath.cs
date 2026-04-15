// Prepends runtimes\win-x64\native to DLL search path so ORT training CUDA build can load bundled cuDNN/cublas.

using System.Runtime.InteropServices;

namespace AutoPBR.Training.Ort;

internal static class WindowsGpuNativePath
{
    public static void PrependIfPresent()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var nativeDir = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        if (!Directory.Exists(nativeDir))
        {
            return;
        }

        try
        {
            SetDllDirectory(nativeDir);
        }
        catch
        {
            // ignore
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? lpPathName);
}
