using Microsoft.ML.OnnxRuntime;

namespace AutoPBR.Core;

internal static class OnnxRuntimeSessionOptions
{
    public static SessionOptions CreateCpuSingleThreaded()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1
        };
        DisableCpuThreadSpinning(options);
        return options;
    }

    public static void ApplyGraphOptimizations(SessionOptions options)
    {
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        DisableCpuThreadSpinning(options);
    }

    /// <summary>
    /// ONNX Runtime worker threads spin by default to optimize latency. In AutoPBR these
    /// sessions stay cached after preview/explore ML, so spinning permanently steals CPU
    /// from the high-FPS OpenGL preview. Disable spin-wait for interactive app workloads.
    /// </summary>
    public static void DisableCpuThreadSpinning(SessionOptions options)
    {
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
    }
}
