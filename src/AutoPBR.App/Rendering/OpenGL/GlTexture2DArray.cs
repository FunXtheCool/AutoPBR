using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlTexture2DArray : IDisposable
{
    private readonly GL _gl;
    private readonly uint _id;
    private bool _disposed;
    private int _width;
    private int _height;
    private int _layers;
    private bool _nearest;
    private ulong _fingerprint;
    private bool _hasCache;

    public GlTexture2DArray(GL gl)
    {
        _gl = gl;
        _id = _gl.GenTexture();
    }

    public uint Id => _id;

    public int Width => _width;

    public int Height => _height;

    public int Layers => _layers;

    public void Bind(uint unit)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2DArray, _id);
    }

    public bool UploadRgbaIfChanged(int width, int height, int layers, ReadOnlySpan<byte> rgba, bool nearest)
    {
        var expected = width * height * layers * 4;
        if (width <= 0 || height <= 0 || layers <= 0 || rgba.Length < expected)
        {
            return false;
        }

        var upload = rgba[..expected];
        var fingerprint = GlRgbaFingerprint.Compute(upload);
        if (_hasCache &&
            _width == width &&
            _height == height &&
            _layers == layers &&
            _nearest == nearest &&
            _fingerprint == fingerprint)
        {
            return false;
        }

        Bind(0);
        ApplyFilter(nearest);
        unsafe
        {
            fixed (byte* ptr = upload)
            {
                if (_hasCache && _width == width && _height == height && _layers == layers)
                {
                    _gl.TexSubImage3D(
                        TextureTarget.Texture2DArray,
                        0,
                        0,
                        0,
                        0,
                        (uint)width,
                        (uint)height,
                        (uint)layers,
                        PixelFormat.Rgba,
                        PixelType.UnsignedByte,
                        ptr);
                }
                else
                {
                    _gl.TexImage3D(
                        TextureTarget.Texture2DArray,
                        0,
                        InternalFormat.Rgba8,
                        (uint)width,
                        (uint)height,
                        (uint)layers,
                        0,
                        PixelFormat.Rgba,
                        PixelType.UnsignedByte,
                        ptr);
                }
            }
        }

        _width = width;
        _height = height;
        _layers = layers;
        _nearest = nearest;
        _fingerprint = fingerprint;
        _hasCache = true;
        return true;
    }

    private void ApplyFilter(bool nearest)
    {
        var filter = nearest ? (int)GLEnum.Nearest : (int)GLEnum.Linear;
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, filter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, filter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _gl.DeleteTexture(_id);
        }
        catch (Exception)
        {
            // Context may already be destroyed during teardown.
        }
    }
}
