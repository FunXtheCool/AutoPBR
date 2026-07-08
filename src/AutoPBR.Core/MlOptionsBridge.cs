using AutoPBR.Contracts.Ml;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

internal static class MlOptionsBridge
{
    public static MlSpecularPathOptions ToMlSpecularPathOptions(this AutoPBROptions options) =>
        new(
            options.UseMlSpecularPredictor,
            options.MlSpecularModelPath,
            options.MlSpecularModelPathsByResolution);

    public static MlRuntimePreferences ToMlRuntimePreferences(this AutoPBROptions options) =>
        new(
            options.PreferOnnxTensorRtExecutionProvider,
            options.MlSpecularUseEdgeChannel,
            options.SpecularDebugVerboseSpecularMl,
            Math.Clamp(ThreadingUtil.GetConversionParallelism(options), 1, 4));
}
