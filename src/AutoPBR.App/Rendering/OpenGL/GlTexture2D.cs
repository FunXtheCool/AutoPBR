using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlTexture2D : IDisposable
{
    private readonly GL _gl;
    private readonly uint _id;
    private bool _disposed;

    public GlTexture2D(GL gl, bool nearestFilter = true)
    {
        _gl = gl;
        _id = _gl.GenTexture();
        Bind(0);
        ApplyFilter(nearestFilter);
        UploadRgba(1, 1, [255, 255, 255, 255], nearestFilter);
    }

    public uint Id => _id;

    public void Bind(uint unit)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2D, _id);
    }

    private void ApplyFilter(bool nearestFilter)
    {
        var mag = nearestFilter ? (int)GLEnum.Nearest : (int)GLEnum.Linear;
        var min = nearestFilter ? (int)GLEnum.Nearest : (int)GLEnum.Linear;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, min);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, mag);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
    }

    public void UploadRgba(int width, int height, ReadOnlySpan<byte> rgba, bool nearestFilter = true)
    {
        Bind(0);
        ApplyFilter(nearestFilter);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, rgba);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gl.DeleteTexture(_id);
    }
}
