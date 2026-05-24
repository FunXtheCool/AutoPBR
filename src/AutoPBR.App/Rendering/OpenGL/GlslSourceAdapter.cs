using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Adapts desktop GLSL 3.30 sources for OpenGL ES 3.0 (e.g. Avalonia/ANGLE on Windows).</summary>
internal static class GlslSourceAdapter
{
    private const string DesktopVersion = "#version 330 core";

    public static string Adapt(string source, ShaderType type, bool useOpenGlEs)
    {
        if (!useOpenGlEs)
        {
            return source;
        }

        if (!TryStripDesktopVersionHeader(source, out var body))
        {
            return source;
        }

        var prec = type == ShaderType.FragmentShader
            ? "precision highp float;\nprecision highp int;\nprecision highp sampler2DShadow;\n"
            // Vertex skinning uses int bone indices + UBO scalars; default int precision on GLES can be too low for ANGLE.
            : "precision highp float;\nprecision highp int;\n";

        return "#version 300 es\n" + prec + body;
    }

    private static bool TryStripDesktopVersionHeader(string source, out string remainder)
    {
        var trimmed = source.TrimStart();
        if (!trimmed.StartsWith(DesktopVersion, StringComparison.Ordinal))
        {
            remainder = source;
            return false;
        }

        var after = trimmed[DesktopVersion.Length..].TrimStart('\r', '\n');
        remainder = after;
        return true;
    }
}
