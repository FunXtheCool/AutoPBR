using System.Text;

using JetBrains.Annotations;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlShaderProgram : IDisposable
{
    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    internal GlShaderProgram(GL gl, uint program)
    {
        _gl = gl;
        _program = program;
    }

    [UsedImplicitly]
    public uint Program => _program;
    public bool IsValid => _program != 0;

    public void Use() => _gl.UseProgram(_program);

    public int GetUniformLocation(string name) => _gl.GetUniformLocation(_program, name);

    internal static void AppendShaderFingerprint(StringBuilder sb, string source)
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

    internal static void AppendShaderDumpHint(StringBuilder sb, ShaderType type, string debugLabel, string source)
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

    internal static void AppendShaderContext(StringBuilder sb, string source, string infoLog)
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
