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
        return options;
    }

    public static void ApplyGraphOptimizations(SessionOptions options)
    {
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
    }
}
