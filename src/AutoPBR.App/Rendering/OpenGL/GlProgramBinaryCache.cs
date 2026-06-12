namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Persists linked program binaries under %LocalAppData%/AutoPBR/shader-cache.</summary>
internal sealed class GlProgramBinaryCache
{
    private readonly string _rootDir;

    public GlProgramBinaryCache(string cacheIdentity)
    {
        var safe = string.Join('_', cacheIdentity.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        _rootDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoPBR",
            "shader-cache",
            safe);
    }

    public bool TryLoad(string programKey, out uint format, out byte[] binary)
    {
        format = 0;
        binary = Array.Empty<byte>();
        var binPath = Path.Combine(_rootDir, programKey + ".bin");
        var fmtPath = Path.Combine(_rootDir, programKey + ".fmt");
        if (!File.Exists(binPath) || !File.Exists(fmtPath))
        {
            return false;
        }

        try
        {
            var fmtText = File.ReadAllText(fmtPath).Trim();
            if (!uint.TryParse(fmtText, System.Globalization.NumberStyles.HexNumber, null, out format))
            {
                return false;
            }

            binary = File.ReadAllBytes(binPath);
            return binary.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ClearAll()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoPBR",
                "shader-cache");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch
        {
            // Best-effort cache only.
        }
    }

    public void TryStore(string programKey, uint format, ReadOnlySpan<byte> binary)
    {
        if (binary.IsEmpty)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_rootDir);
            var binPath = Path.Combine(_rootDir, programKey + ".bin");
            var fmtPath = Path.Combine(_rootDir, programKey + ".fmt");
            File.WriteAllBytes(binPath, binary.ToArray());
            File.WriteAllText(fmtPath, format.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            // Best-effort cache only.
        }
    }
}
