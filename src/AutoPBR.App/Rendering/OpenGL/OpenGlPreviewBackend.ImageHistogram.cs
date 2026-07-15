namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _imageHistogramProgram;
    private GlImageLuminanceHistogram? _imageHistogram;
    private bool _imageHistogramCompileDisabled;

    private bool TryCaptureGpuLuminanceHistogram(out GlLuminanceHistogramSnapshot snapshot)
    {
        snapshot = new GlLuminanceHistogramSnapshot(new uint[GlLuminanceHistogramSnapshot.BinCount], 0, 0);
        if (_gl is null || _shaderCtx is null || _sceneCapture is not { IsValid: true } ||
            _glCapabilities?.CanUseImageHistogram != true || _imageHistogramCompileDisabled)
        {
            return false;
        }

        if (_imageHistogramProgram is not { IsValid: true })
        {
            _imageHistogramProgram?.Dispose();
            _imageHistogramProgram = CreatePreviewComputeProgram(
                "genesis_luminance_histogram.comp",
                out var error,
                "p61-luminance-histogram");
            if (_imageHistogramProgram is not { IsValid: true })
            {
                _imageHistogramCompileDisabled = true;
                EmitDiagnostic("[3D preview] P6.1 image histogram fallback: " + TrimShaderDiagnostic(error));
                _imageHistogramProgram?.Dispose();
                _imageHistogramProgram = null;
                return false;
            }
        }

        _imageHistogram ??= new GlImageLuminanceHistogram(_gl);
        return _imageHistogram.Dispatch(
            _imageHistogramProgram,
            _sceneCapture.ColorTextureHandle,
            _sceneCapture.Width,
            _sceneCapture.Height,
            out snapshot);
    }

    private void DestroyImageHistogramResources()
    {
        _imageHistogram?.Dispose();
        _imageHistogram = null;
        _imageHistogramProgram?.Dispose();
        _imageHistogramProgram = null;
        _imageHistogramCompileDisabled = false;
    }

    private void AbandonImageHistogramResources()
    {
        _imageHistogram = null;
        _imageHistogramProgram = null;
        _imageHistogramCompileDisabled = false;
    }
}
