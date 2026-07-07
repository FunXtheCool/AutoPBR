using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class GlFramebufferReadback
{
    public static byte[]? TryReadRgb8(GL gl, int x, int y, int width, int height)
    {
        return TryReadRgb8(gl, x, y, width, height, out _);
    }

    public static byte[]? TryReadRgb8(GL gl, int x, int y, int width, int height, out GLEnum error)
    {
        if (width <= 0 || height <= 0)
        {
            error = GLEnum.InvalidValue;
            return null;
        }

        DrainErrors(gl);
        error = GLEnum.NoError;
        var priorPack = gl.GetInteger(GetPName.PackAlignment);
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        var rgba = new byte[width * height * 4];
        try
        {
            unsafe
            {
                fixed (byte* ptr = rgba)
                {
                    gl.ReadPixels(x, y, (uint)width, (uint)height, GLEnum.Rgba, GLEnum.UnsignedByte, ptr);
                }
            }

            error = gl.GetError();
            if (error != GLEnum.NoError)
            {
                return null;
            }

            var rgb = new byte[width * height * 3];
            for (var src = 0; src < rgba.Length; src += 4)
            {
                var dst = (src / 4) * 3;
                rgb[dst] = rgba[src];
                rgb[dst + 1] = rgba[src + 1];
                rgb[dst + 2] = rgba[src + 2];
            }

            return rgb;
        }
        finally
        {
            gl.PixelStore(PixelStoreParameter.PackAlignment, priorPack);
        }
    }

    private static void DrainErrors(GL gl)
    {
        for (var i = 0; i < 16; i++)
        {
            if (gl.GetError() == GLEnum.NoError)
            {
                return;
            }
        }
    }
}
