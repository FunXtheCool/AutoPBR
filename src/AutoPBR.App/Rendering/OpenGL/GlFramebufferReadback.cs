using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class GlFramebufferReadback
{
    public static byte[]? TryReadRgb8(GL gl, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var priorPack = gl.GetInteger(GetPName.PackAlignment);
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        var bytes = new byte[width * height * 3];
        try
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    gl.ReadPixels(x, y, (uint)width, (uint)height, GLEnum.Rgb, GLEnum.UnsignedByte, ptr);
                }
            }

            return gl.GetError() == GLEnum.NoError ? bytes : null;
        }
        finally
        {
            gl.PixelStore(PixelStoreParameter.PackAlignment, priorPack);
        }
    }
}
