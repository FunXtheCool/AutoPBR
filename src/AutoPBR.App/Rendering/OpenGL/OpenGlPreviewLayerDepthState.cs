using AutoPBR.Core.Models;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Scoped OpenGL depth state for preview draw batches with overlay layer policies.
/// Bias sign is for standard OpenGL <c>GL_LEQUAL</c>; flip when reversed-Z is adopted.
/// </summary>
internal static class OpenGlPreviewLayerDepthState
{
    private const float BiasFactorPerStep = -0.25f;
    private const float BiasUnitsPerStep = -1f;

    public static LayerDepthScope Apply(GL gl, PreviewDrawLayerPolicy policy, bool reversedZ = false)
    {
        return new LayerDepthScope(gl, policy, reversedZ);
    }

    internal sealed class LayerDepthScope : IDisposable
    {
        private readonly GL _gl;
        private readonly bool _restoreDepthMask;
        private readonly bool _restorePolygonOffset;

        public LayerDepthScope(GL gl, PreviewDrawLayerPolicy policy, bool reversedZ)
        {
            _gl = gl;
            _restoreDepthMask = !policy.DepthWrite;
            _gl.DepthMask(policy.DepthWrite);

            if (policy.DepthBiasStep > 0)
            {
                _restorePolygonOffset = true;
                var sign = reversedZ ? -1f : 1f;
                var step = policy.DepthBiasStep;
                _gl.Enable(EnableCap.PolygonOffsetFill);
                _gl.PolygonOffset(sign * BiasFactorPerStep * step, sign * BiasUnitsPerStep * step);
            }
        }

        public void Dispose()
        {
            if (_restorePolygonOffset)
            {
                _gl.Disable(EnableCap.PolygonOffsetFill);
            }

            if (_restoreDepthMask)
            {
                _gl.DepthMask(true);
            }
        }
    }
}
