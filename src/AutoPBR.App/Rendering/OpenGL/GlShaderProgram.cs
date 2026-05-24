using System.Text;

using Avalonia.Platform;

using JetBrains.Annotations;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlShaderProgram : IDisposable
{
    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    public GlShaderProgram(GL gl, string vertexFile, string fragmentFile, bool useOpenGlEs, out string? error)
    {
        _gl = gl;
        error = null;
        _program = 0;
        string vSrc;
        string fSrc;
        try
        {
            // Flatten //!include directives BEFORE the version adapter so include bodies
            // never carry their own #version line; only the entry .vert/.frag does.
            vSrc = GlslSourceAdapter.Adapt(
                GlslIncludeResolver.Resolve(vertexFile, LoadShader), ShaderType.VertexShader, useOpenGlEs);
            fSrc = GlslSourceAdapter.Adapt(
                GlslIncludeResolver.Resolve(fragmentFile, LoadShader), ShaderType.FragmentShader, useOpenGlEs);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return;
        }

        var vs = Compile(ShaderType.VertexShader, vSrc, ref error);
        if (vs == 0)
        {
            return;
        }

        var fs = Compile(ShaderType.FragmentShader, fSrc, ref error);
        if (fs == 0)
        {
            _gl.DeleteShader(vs);
            return;
        }

        _program = _gl.CreateProgram();
        _gl.AttachShader(_program, vs);
        _gl.AttachShader(_program, fs);
        _gl.LinkProgram(_program);
        _gl.GetProgram(_program, GLEnum.LinkStatus, out var ok);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        if (ok == 0)
        {
            var linkLog = _gl.GetProgramInfoLog(_program);
            error = string.IsNullOrEmpty(error) ? linkLog : error + "\n" + linkLog;
            _gl.DeleteProgram(_program);
            _program = 0;
        }
    }

    [UsedImplicitly]
    public uint Program => _program;
    public bool IsValid => _program != 0;

    public void Use() => _gl.UseProgram(_program);

    public int GetUniformLocation(string name) => _gl.GetUniformLocation(_program, name);

    private uint Compile(ShaderType type, string source, ref string? error)
    {
        var s = _gl.CreateShader(type);
        _gl.ShaderSource(s, source);
        _gl.CompileShader(s);
        _gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok == 0)
        {
            var info = _gl.GetShaderInfoLog(s);
            var sb = new StringBuilder();
            sb.Append(type).Append(" compile failed: ").AppendLine(info);
            error = string.IsNullOrEmpty(error) ? sb.ToString() : error + "\n" + sb;
            _gl.DeleteShader(s);
            return 0;
        }

        return s;
    }

    private static string LoadShader(string fileName)
    {
        var uri = new Uri($"avares://AutoPBR.App/Rendering/Shaders/{fileName}");
        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
            _program = 0;
        }
    }
}
