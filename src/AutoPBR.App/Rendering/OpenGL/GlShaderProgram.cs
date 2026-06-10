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
        : this(gl, vertexFile, fragmentFile, useOpenGlEs, out error, null)
    {
    }

    public GlShaderProgram(GL gl, string vertexFile, string fragmentFile, bool useOpenGlEs, out string? error,
        string? debugLabel)
    {
        _gl = gl;
        error = null;
        _program = 0;
        var label = string.IsNullOrWhiteSpace(debugLabel) ? fragmentFile : debugLabel;
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

        var vs = Compile(ShaderType.VertexShader, vSrc, label, ref error);
        if (vs == 0)
        {
            return;
        }

        var fs = Compile(ShaderType.FragmentShader, fSrc, label, ref error);
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

    private uint Compile(ShaderType type, string source, string debugLabel, ref string? error)
    {
        var s = _gl.CreateShader(type);
        _gl.ShaderSource(s, source);
        _gl.CompileShader(s);
        _gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok == 0)
        {
            var info = _gl.GetShaderInfoLog(s);
            var sb = new StringBuilder();
            sb.Append(type).Append(" compile failed (").Append(debugLabel).Append("): ").AppendLine(info);
            AppendShaderContext(sb, source, info);
            AppendShaderFingerprint(sb, source);
            AppendShaderDumpHint(sb, type, debugLabel, source);
            error = string.IsNullOrEmpty(error) ? sb.ToString() : error + "\n" + sb;
            _gl.DeleteShader(s);
            return 0;
        }

        return s;
    }

    private static void AppendShaderFingerprint(StringBuilder sb, string source)
    {
        var packLine = source.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.Contains("GENESIS_GLES_PACK", StringComparison.Ordinal));
        sb.Append("Shader pack: ").AppendLine(string.IsNullOrEmpty(packLine) ? "(missing marker)" : packLine.Trim());
        sb.Append("Fingerprint texture(uFroxelVolume: ")
            .Append(source.Contains("texture(uFroxelVolume", StringComparison.Ordinal) ? "yes" : "no")
            .Append(", texelFetch(uFroxelVolume: ")
            .Append(source.Contains("texelFetch(uFroxelVolume", StringComparison.Ordinal) ? "yes" : "no")
            .Append(", viMarch8Texture: ")
            .Append(source.Contains("viMarch8Texture", StringComparison.Ordinal) ? "yes" : "no")
            .Append(", for+texture(froxel: ")
            .Append(System.Text.RegularExpressions.Regex.IsMatch(source, @"for\s*\([^)]*\)[\s\S]*texture\s*\(\s*uFroxelVolume", System.Text.RegularExpressions.RegexOptions.None) ? "yes" : "no")
            .AppendLine(", lines: " + source.Split('\n').Length);
    }

    private static void AppendShaderDumpHint(StringBuilder sb, ShaderType type, string debugLabel, string source)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "AutoPBR-shader-dumps");
            Directory.CreateDirectory(dir);
            var safeLabel = string.Join("_", debugLabel.Split(Path.GetInvalidFileNameChars()));
            var name = $"failed-{safeLabel}-{type}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.glsl";
            var path = Path.Combine(dir, name);
            File.WriteAllText(path, source);
            sb.Append("Adapted shader dump: ").AppendLine(path);
        }
        catch
        {
            // Best-effort debug aid only.
        }
    }

    private static void AppendShaderContext(StringBuilder sb, string source, string infoLog)
    {
        var lineMatch = System.Text.RegularExpressions.Regex.Match(infoLog, @":(\d+):");
        if (!lineMatch.Success || !int.TryParse(lineMatch.Groups[1].Value, out var lineNo) || lineNo < 1)
        {
            return;
        }

        var lines = source.Split('\n');
        sb.AppendLine("Source context:");
        for (var i = Math.Max(0, lineNo - 3); i < Math.Min(lines.Length, lineNo + 2); i++)
        {
            sb.Append(i + 1).Append(": ").AppendLine(lines[i].TrimEnd('\r'));
        }
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
