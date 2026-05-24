using System.Text;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Tiny preprocessor that resolves <c>//!include "name"</c> directives in GLSL source.
/// GLSL 330 core / GLSL ES 300 do not have a portable native <c>#include</c> (the
/// <c>GL_ARB_shading_language_include</c> extension is driver-optional and absent on ANGLE),
/// so we flatten includes in C# before passing the source to <see cref="GlslSourceAdapter"/>.
/// </summary>
/// <remarks>
/// Directive grammar: an entire line whose trimmed contents start with the literal token
/// <c>//!include</c> followed by whitespace and a quoted file name. One include per line.
/// Lines that fail to parse fall through unchanged so editors still see them as comments.
/// Includes resolve relative to the parent file's directory; an include name beginning with
/// <c>/</c> is treated as relative to the shaders root.
/// </remarks>
internal static class GlslIncludeResolver
{
    private const string Directive = "//!include";
    public const int MaxIncludeDepth = 8;

    /// <summary>Resolves all includes in the entry shader and returns the flattened source.</summary>
    /// <param name="entryFileName">Shader name relative to the shaders root (e.g. <c>genesis.frag</c>).</param>
    /// <param name="readFile">Reads a shader file's raw text by name (relative to the shaders root).</param>
    /// <remarks>
    /// The entry file's body is emitted WITHOUT a surrounding begin/end marker so that any leading
    /// <c>#version</c> directive remains the first non-whitespace token (required by the GLSL ES
    /// version-strip pass in <see cref="GlslSourceAdapter"/> and by strict drivers).
    /// </remarks>
    public static string Resolve(string entryFileName, Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(readFile);
        if (string.IsNullOrWhiteSpace(entryFileName))
        {
            throw new ArgumentException("Entry file name must be non-empty.", nameof(entryFileName));
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder();
        var inclusionDepth = 0;

        void AppendRecursive(string fileName, string parentDir, bool isEntry)
        {
            if (inclusionDepth > MaxIncludeDepth)
            {
                throw new InvalidOperationException(
                    $"GLSL include depth exceeded {MaxIncludeDepth} while including '{fileName}'.");
            }

            var resolvedName = NormalizeIncludePath(fileName, parentDir);
            if (!visited.Add(resolvedName))
            {
                // Already included once; suppress to behave like a single-pass include guard.
                output.Append("// --- skip duplicate include: ").Append(resolvedName).Append(" ---\n");
                return;
            }

            string source;
            try
            {
                source = readFile(resolvedName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"GLSL include '{resolvedName}' could not be loaded: {ex.Message}", ex);
            }

            var thisDir = GetDirectory(resolvedName);
            if (!isEntry)
            {
                output.Append("// --- begin ").Append(resolvedName).Append(" ---\n");
            }

            var lines = source.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
                if (TryParseIncludeDirective(line, out var includedName))
                {
                    inclusionDepth++;
                    try
                    {
                        AppendRecursive(includedName, thisDir, isEntry: false);
                    }
                    finally
                    {
                        inclusionDepth--;
                    }

                    continue;
                }

                output.Append(line).Append('\n');
            }

            if (!isEntry)
            {
                output.Append("// --- end ").Append(resolvedName).Append(" ---\n");
            }
        }

        AppendRecursive(entryFileName, parentDir: string.Empty, isEntry: true);
        return output.ToString();
    }

    private static bool TryParseIncludeDirective(string line, out string includedName)
    {
        includedName = string.Empty;
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(Directive, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = trimmed[Directive.Length..];
        if (rest.Length == 0 || (rest[0] != ' ' && rest[0] != '\t'))
        {
            return false;
        }

        rest = rest.TrimStart();
        if (rest.Length < 2 || rest[0] != '"')
        {
            return false;
        }

        var closeIdx = rest.IndexOf('"', 1);
        if (closeIdx < 0)
        {
            return false;
        }

        var name = rest.Substring(1, closeIdx - 1).Trim();
        if (name.Length == 0)
        {
            return false;
        }

        includedName = name;
        return true;
    }

    private static string NormalizeIncludePath(string includeName, string parentDir)
    {
        var n = includeName.Replace('\\', '/').Trim();
        if (n.StartsWith('/'))
        {
            return n.TrimStart('/');
        }

        if (string.IsNullOrEmpty(parentDir))
        {
            return n;
        }

        return parentDir + "/" + n;
    }

    private static string GetDirectory(string normalizedName)
    {
        var i = normalizedName.LastIndexOf('/');
        return i < 0 ? string.Empty : normalizedName[..i];
    }
}
