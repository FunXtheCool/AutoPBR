// Ensures bundled CUDA/cuDNN/TensorRT DLLs in runtimes\win-x64\native are found before ONNX Runtime
// loads GPU execution providers. Without this, Windows may pick a system cudnn from PATH and fail with
// e.g. "Invalid handle. Cannot load symbol cudnnCreate".

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;

namespace AutoPBR.Core;

/// <summary>
/// Windows-only: prepends the app's ONNX GPU native folder to the DLL search path so
/// <c>cudnn*.dll</c> / <c>cublas*.dll</c> / TensorRT <c>nvinfer*.dll</c> match the ONNX Runtime 1.24.x GPU build.
/// </summary>
internal static class OnnxRuntimeWindowsNative
{
    private const int DefaultGpuDeviceId = 0;

    /// <summary>Adds <c>runtimes\win-x64\native</c> under <see cref="AppContext.BaseDirectory"/> to the DLL search path.</summary>
    public static void EnsureBundledGpuNativeOnPath()
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
            // Ignore if SetDllDirectory fails
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? lpPathName);

    /// <summary>
    /// Disk cache for TensorRT compiled engines / timing data so repeat loads are not dominated by engine build.
    /// </summary>
    private static string GetTensorRtCacheDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoPBR",
            "onnx_tensorrt_cache");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Tries to create a GPU-backed ONNX Runtime session.
    /// When <paramref name="preferTensorRtExecutionProvider"/> is false (default), uses CUDA only.
    /// When true, registers TensorRT (with CUDA for unsupported subgraphs), then falls back to CUDA-only on failure.
    /// Returns <c>null</c> if no GPU session could be created (caller falls back to CPU).
    /// </summary>
    public static InferenceSession? TryCreateGpuSession(
        string modelPath,
        bool preferTensorRtExecutionProvider,
        out string provider,
        out string? diagnostic)
    {
        provider = "CPU";
        diagnostic = null;

        EnsureBundledGpuNativeOnPath();

        if (!preferTensorRtExecutionProvider)
        {
            try
            {
                using var cudaOptions = SessionOptions.MakeSessionOptionWithCudaProvider();
                OnnxRuntimeSessionOptions.ApplyGraphOptimizations(cudaOptions);
                provider = "CUDA";
                return new InferenceSession(modelPath, cudaOptions);
            }
            catch (Exception exCuda)
            {
                diagnostic = $"CUDA session failed: {exCuda.Message}";
                return null;
            }
        }

        try
        {
            using var so = new SessionOptions();
            OnnxRuntimeSessionOptions.ApplyGraphOptimizations(so);
            var cacheDir = GetTensorRtCacheDirectory();
            using (var trtOpts = new OrtTensorRTProviderOptions())
            {
                trtOpts.UpdateOptions(new Dictionary<string, string>
                {
                    ["device_id"] = DefaultGpuDeviceId.ToString(CultureInfo.InvariantCulture),
                    ["trt_engine_cache_enable"] = "1",
                    ["trt_engine_cache_path"] = cacheDir,
                    ["trt_timing_cache_enable"] = "1",
                    ["trt_timing_cache_path"] = cacheDir,
                });
                so.AppendExecutionProvider_Tensorrt(trtOpts);
            }

            so.AppendExecutionProvider_CUDA();
            provider = "TensorRT";
            return new InferenceSession(modelPath, so);
        }
        catch (Exception exTrtCuda)
        {
            try
            {
                using var cudaOptions = SessionOptions.MakeSessionOptionWithCudaProvider();
                OnnxRuntimeSessionOptions.ApplyGraphOptimizations(cudaOptions);
                provider = "CUDA";
                return new InferenceSession(modelPath, cudaOptions);
            }
            catch (Exception exCuda)
            {
                diagnostic =
                    $"TensorRT+CUDA session failed: {exTrtCuda.Message}; CUDA-only session failed: {exCuda.Message}";
                return null;
            }
        }
    }
}
