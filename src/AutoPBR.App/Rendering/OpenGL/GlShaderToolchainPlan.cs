namespace AutoPBR.App.Rendering.OpenGL;

internal sealed record GlShaderToolchainPlan(
    bool IsOpenGlEs,
    bool SpirVSupported,
    bool SeparableProgramsSupported,
    int SpirVAssetCount)
{
    public const string PrimaryPath = "GLSL source + program binary cache";

    public bool CanUseSpirVAssets => !IsOpenGlEs && SpirVSupported && SpirVAssetCount > 0;

    public bool CanEvaluateSeparablePrograms => !IsOpenGlEs && SeparableProgramsSupported;

    public string SpirVStatus =>
        IsOpenGlEs ? "off-gles" :
        !SpirVSupported ? "unsupported" :
        SpirVAssetCount <= 0 ? "no-assets" :
        "ready";

    public string SeparableProgramStatus =>
        IsOpenGlEs ? "off-gles" :
        SeparableProgramsSupported ? "available" :
        "unsupported";

    public string FormatDiagnostic() =>
        "[3D preview] Shader toolchain: " +
        $"primary={PrimaryPath}; " +
        $"spirv={SpirVStatus}; " +
        $"spirvAssets={SpirVAssetCount}; " +
        $"separablePrograms={SeparableProgramStatus}; " +
        "fallback=GLSL.";

    public string FormatContextSuffix()
    {
        var spirv = CanUseSpirVAssets ? "SPIR-V assets ready" :
            SpirVSupported && !IsOpenGlEs ? "SPIR-V staged" : "no SPIR-V";
        var separable = CanEvaluateSeparablePrograms ? "separable available" : "monolithic programs";
        return $" · GLSL primary · {spirv} · {separable}";
    }

    public static GlShaderToolchainPlan FromCapabilities(
        PreviewGlCapabilities capabilities,
        int spirvAssetCount = 0) =>
        new(
            capabilities.IsOpenGlEs,
            capabilities.SpirV,
            capabilities.SeparablePrograms,
            Math.Max(0, spirvAssetCount));
}
