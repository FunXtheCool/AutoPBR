using System.Text;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Textured moon disc billboard opposite the sun light direction.</summary>
internal sealed class GlMoonBillboardProgram : IDisposable
{
    private const string Vert330 = """
#version 330 core
layout(location = 0) in vec2 aCorner;
uniform mat4 uViewProj;
uniform vec3 uMoonCenter;
uniform vec3 uMoonRight;
uniform vec3 uMoonUp;
uniform float uRadius;
out vec2 vTexCoord;
void main()
{
    vTexCoord = aCorner * 0.5 + 0.5;
    vec3 worldPos = uMoonCenter + uMoonRight * (aCorner.x * uRadius) + uMoonUp * (aCorner.y * uRadius);
    gl_Position = uViewProj * vec4(worldPos, 1.0);
}
""";

    private const string Frag330 = """
#version 330 core
in vec2 vTexCoord;
uniform sampler2D uMoonAlbedo;
uniform float uDiscStrength;
out vec4 FragColor;
void main()
{
    vec2 centered = vTexCoord * 2.0 - 1.0;
    float d = length(centered);
    if (d > 1.0)
    {
        discard;
    }

    vec3 albedo = texture(uMoonAlbedo, vTexCoord).rgb;
    float limb = 1.0 - smoothstep(0.72, 1.0, d) * 0.38;
    albedo *= limb;
    float strength = max(uDiscStrength, 0.35);
    albedo *= strength;
    float alpha = texture(uMoonAlbedo, vTexCoord).a;
    alpha *= 1.0 - smoothstep(0.92, 1.0, d);
    if (alpha < 0.01)
    {
        discard;
    }

    FragColor = vec4(albedo, alpha);
}
""";

    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    public GlMoonBillboardProgram(GL gl, bool useOpenGlEs, out string? error)
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
