using System.Text;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Central shader compile path: prepared-source cache, disk binary cache, parallel compile.</summary>
internal sealed class GlShaderCompileContext
{
    private readonly GL _gl;
    private readonly bool _useOpenGlEs;
    private readonly string _cacheIdentity;
    private readonly GlProgramBinaryCache _binaryCache;
    private readonly GlParallelShaderCompile _parallelCompile;

    public GlShaderCompileContext(GL gl, bool useOpenGlEs, string vendor, string renderer)
    {
        _gl = gl;
        _useOpenGlEs = useOpenGlEs;
        _cacheIdentity = $"{vendor}|{renderer}|{(useOpenGlEs ? "es" : "gl")}";
        _binaryCache = new GlProgramBinaryCache(_cacheIdentity);
        _parallelCompile = new GlParallelShaderCompile(gl);
    }

    public GlShaderProgram CreateProgram(string vertexFile, string fragmentFile, out string? error,
        string? debugLabel = null)
    {
        error = null;
        var label = string.IsNullOrWhiteSpace(debugLabel) ? fragmentFile : debugLabel;
        var programKey = GlslPreparedSourceCache.ComputeProgramKey(vertexFile, fragmentFile, _useOpenGlEs, _cacheIdentity);

        if (_binaryCache.TryLoad(programKey, out var binaryFormat, out var binaryBytes) &&
            TryLinkFromBinary(binaryBytes, binaryFormat, label, ref error, out var fromCache))
        {
            return fromCache;
        }

        string vSrc;
        string fSrc;
        try
        {
            vSrc = GlslPreparedSourceCache.GetOrPrepare(vertexFile, ShaderType.VertexShader, _useOpenGlEs);
            fSrc = GlslPreparedSourceCache.GetOrPrepare(fragmentFile, ShaderType.FragmentShader, _useOpenGlEs);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return new GlShaderProgram(_gl, 0);
        }

        var vs = CompileShader(ShaderType.VertexShader, vSrc, label, ref error);
        if (vs == 0)
        {
            return new GlShaderProgram(_gl, 0);
        }

        var fs = CompileShader(ShaderType.FragmentShader, fSrc, label, ref error);
        if (fs == 0)
        {
            _gl.DeleteShader(vs);
            return new GlShaderProgram(_gl, 0);
        }

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, GLEnum.LinkStatus, out var ok);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        if (ok == 0)
        {
            var linkLog = _gl.GetProgramInfoLog(program);
            error = string.IsNullOrEmpty(error) ? linkLog : error + "\n" + linkLog;
            _gl.DeleteProgram(program);
            return new GlShaderProgram(_gl, 0);
        }

        TryStoreProgramBinary(program, programKey);
        return new GlShaderProgram(_gl, program);
    }

    private bool TryLinkFromBinary(byte[] binary, uint format, string label, ref string? error,
        out GlShaderProgram program)
    {
        program = new GlShaderProgram(_gl, 0);
        var handle = _gl.CreateProgram();
        unsafe
        {
            fixed (byte* p = binary)
            {
                _gl.ProgramBinary(handle, (GLEnum)format, p, (uint)binary.Length);
            }
        }

        _gl.GetProgram(handle, GLEnum.LinkStatus, out var ok);
        if (ok == 0)
        {
            var linkLog = _gl.GetProgramInfoLog(handle);
            error = $"ProgramBinary reload failed ({label}): {linkLog}";
            _gl.DeleteProgram(handle);
            return false;
        }

        program = new GlShaderProgram(_gl, handle);
        return true;
    }

    private void TryStoreProgramBinary(uint program, string programKey)
    {
        _gl.GetProgram(program, GLEnum.ProgramBinaryLength, out var length);
        if (length <= 0)
        {
            return;
        }

        unsafe
        {
            Span<byte> buffer = length <= 8192 ? stackalloc byte[length] : new byte[length];
            fixed (byte* p = buffer)
            {
                _gl.GetProgramBinary(program, (uint)length, out var written, out var format, p);
                if (written > 0)
                {
                    _binaryCache.TryStore(programKey, (uint)format, buffer[..(int)written]);
                }
            }
        }
    }

    private uint CompileShader(ShaderType type, string source, string debugLabel, ref string? error)
    {
        var s = _gl.CreateShader(type);
        _gl.ShaderSource(s, source);
        _gl.CompileShader(s);
        _parallelCompile.WaitForShader(s);
        _gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok == 0)
        {
            var info = _gl.GetShaderInfoLog(s);
            var sb = new StringBuilder();
            sb.Append(type).Append(" compile failed (").Append(debugLabel).Append("): ").AppendLine(info);
            GlShaderProgram.AppendShaderContext(sb, source, info);
            GlShaderProgram.AppendShaderFingerprint(sb, source);
            GlShaderProgram.AppendShaderDumpHint(sb, type, debugLabel, source);
            error = string.IsNullOrEmpty(error) ? sb.ToString() : error + "\n" + sb;
            _gl.DeleteShader(s);
            return 0;
        }

        return s;
    }
}
