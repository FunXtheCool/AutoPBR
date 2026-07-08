namespace AutoPBR.Contracts.Ml;

/// <summary>ONNX runtime execution preferences for ML inference passes.</summary>
public sealed record MlRuntimePreferences(
    bool PreferOnnxTensorRtExecutionProvider,
    bool MlSpecularUseEdgeChannel,
    bool SpecularDebugVerboseSpecularMl,
    int CpuRunnerPoolSize);
