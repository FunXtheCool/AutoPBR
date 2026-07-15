using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed record GlSpirVShaderBinary(string Name, ShaderType Stage, byte[] Words)
{
    private const uint Magic = 0x07230203;

    public bool IsValid => Words.Length >= 20 && Words.Length % sizeof(uint) == 0 && ReadMagic(Words) == Magic;

    public static bool TryCreate(string name, ShaderType stage, ReadOnlySpan<byte> bytes, out GlSpirVShaderBinary binary)
    {
        binary = new GlSpirVShaderBinary(name, stage, bytes.ToArray());
        return binary.IsValid;
    }

    private static uint ReadMagic(ReadOnlySpan<byte> bytes) =>
        bytes.Length < sizeof(uint)
            ? 0
            : (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
}
