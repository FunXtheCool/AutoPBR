using System.IO.Hashing;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class GlRgbaFingerprint
{
    public static ulong Compute(ReadOnlySpan<byte> rgba)
    {
        var hash = new XxHash64();
        hash.Append(rgba);
        return hash.GetCurrentHashAsUInt64();
    }
}
