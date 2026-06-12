using System.Runtime.InteropServices;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>KHR_parallel_shader_compile when available (ANGLE on Windows often supports it).</summary>
internal sealed class GlParallelShaderCompile
{
    private const int CompletionStatusKhr = 0x91B1;

    private readonly GL _gl;
    public bool IsSupported { get; }

    public GlParallelShaderCompile(GL gl)
    {
        _gl = gl;
        IsSupported = HasExtension(gl, "GL_KHR_parallel_shader_compile");
    }

    public void WaitForShader(uint shader)
    {
        if (!IsSupported)
        {
            return;
        }

        int complete;
        do
        {
            _gl.GetShader(shader, (GLEnum)CompletionStatusKhr, out complete);
        }
        while (complete == 0);
    }

    private static bool HasExtension(GL gl, string extension)
    {
        unsafe
        {
            var p = gl.GetString(StringName.Extensions);
            if (p is null)
            {
                return false;
            }

            var extensions = Marshal.PtrToStringUTF8((nint)p) ?? string.Empty;
            return extensions.Contains(extension, StringComparison.Ordinal);
        }
    }
}
