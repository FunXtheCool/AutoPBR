namespace AutoPBR.Preview;

/// <summary>Reads PNG width/height from IHDR without full decode (entity atlas sizing).</summary>
internal static class EntityTexturePngDimensions
{
    public static bool TryRead(string absolutePath, out int width, out int height)
    {
        width = height = 0;
        try
        {
            if (!File.Exists(absolutePath))
            {
                return false;
            }

            using var fs = File.OpenRead(absolutePath);
            Span<byte> head = stackalloc byte[24];
            if (fs.Read(head) < 24)
            {
                return false;
            }

            if (head[0] != 0x89 || head[1] != 0x50 || head[2] != 0x4E || head[3] != 0x47 ||
                head[4] != 0x0D || head[5] != 0x0A || head[6] != 0x1A || head[7] != 0x0A)
            {
                return false;
            }

            // IHDR at offset 8; width/height big-endian at 16–23.
            width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }
}
