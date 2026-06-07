using System.Text;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Unlit additive-style sun disc for the Genesis preview; faces the camera on the light yaw/pitch ray.</summary>
internal sealed class GlSunBillboardProgram : IDisposable
{
    private const string Vert330 = """
#version 330 core
layout(location = 0) in vec2 aCorner;
uniform mat4 uViewProj;
uniform vec3 uSunCenter;
uniform vec3 uSunRight;
uniform vec3 uSunUp;
uniform float uRadius;
uniform float uDiscStrength;
out vec2 vDiscCoord;
void main()
{
    vDiscCoord = aCorner;
    vec3 worldPos = uSunCenter + uSunRight * (aCorner.x * uRadius) + uSunUp * (aCorner.y * uRadius);
    gl_Position = uViewProj * vec4(worldPos, 1.0);
}
""";

    private const string Frag330 = """
#version 330 core
in vec2 vDiscCoord;
uniform float uDiscStrength;
out vec4 FragColor;
void main()
{
    float d = length(vDiscCoord);
    if (d > 1.0)
    {
        discard;
    }

    float strength = max(uDiscStrength, 0.0);
    float core = 1.0 - smoothstep(0.06, 0.48, d);
    float halo = exp(-max(d - 0.28, 0.0) * 7.5) * 0.72;
    vec3 rgb = vec3(1.0, 0.96, 0.84) * (core * 4.2 + halo) * max(strength, 0.35);
    float alpha = clamp(core * 0.96 + halo * 0.42, 0.0, 1.0);
    if (alpha < 0.004)
    {
        discard;
    }

    FragColor = vec4(rgb, alpha);
}
""";

    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    public GlSunBillboardProgram(GL gl, bool useOpenGlEs, out string? error)
    {
        _gl = gl;
        error = null;
        _program = 0;
        var vSrc = GlslSourceAdapter.Adapt(Vert330, ShaderType.VertexShader, useOpenGlEs);
        var fSrc = GlslSourceAdapter.Adapt(Frag330, ShaderType.FragmentShader, useOpenGlEs);
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
