using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Camera-aligned froxel volume stored in a 2D texture array (P3).</summary>
internal sealed class GlVolumeFroxelTarget(GL gl, bool useOpenGlEs) : IDisposable
{
    public const int DefaultSliceCount = 24;

    private uint _fbo;
    private uint _arrayTexture;
    private uint _occupancyTexture;
    private int _width;
    private int _height;
    private int _slices;
    private bool _disposed;

    public uint ArrayTextureHandle => _arrayTexture;
    public uint OccupancyTextureHandle => _occupancyTexture;
    public int Width => _width;
    public int Height => _height;
    public int Slices => _slices;
    public bool IsValid => _fbo != 0 && _arrayTexture != 0 && _occupancyTexture != 0 && _slices > 0;

    public bool EnsureSize(int width, int height, int slices = DefaultSliceCount)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        slices = Math.Clamp(slices, 8, DefaultSliceCount);
        if (_width == width && _height == height && _slices == slices && IsValid)
        {
            return true;
        }

        DestroyGpuResources();
        _width = width;
        _height = height;
        _slices = slices;

        _arrayTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2DArray, _arrayTexture);
        unsafe
        {
            gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba8, (uint)width, (uint)height, (uint)slices,
                0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
        }

        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);

        _occupancyTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2DArray, _occupancyTexture);
        unsafe
        {
            gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.R8, (uint)width, (uint)height, (uint)slices,
                0, PixelFormat.Red, PixelType.UnsignedByte, (void*)0);
        }

        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);

        _fbo = gl.GenFramebuffer();
        gl.BindTexture(TextureTarget.Texture2DArray, 0);
        return IsValid;
    }

    public bool BindDrawLayer(int layer)
    {
        if (!IsValid || layer < 0 || layer >= _slices)
        {
            return false;
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            _arrayTexture, 0, layer);
        gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
            _occupancyTexture, 0, layer);
        ConfigureColorAttachments();
        gl.Viewport(0, 0, (uint)_width, (uint)_height);
        return gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
    }

    public bool BindImagesForCompute(uint froxelImageUnit, uint occupancyImageUnit)
    {
        if (!IsValid)
        {
            return false;
        }

        gl.BindImageTexture(froxelImageUnit, _arrayTexture, 0, true, 0, GLEnum.WriteOnly, GLEnum.Rgba8);
        gl.BindImageTexture(occupancyImageUnit, _occupancyTexture, 0, true, 0, GLEnum.WriteOnly, GLEnum.R8);
        return true;
    }

    public void Unbind()
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void ConfigureColorAttachments()
    {
        if (useOpenGlEs)
        {
            unsafe
            {
                var bufs = stackalloc DrawBufferMode[]
                {
                    DrawBufferMode.ColorAttachment0,
                    DrawBufferMode.ColorAttachment1,
                };
                gl.DrawBuffers(2, bufs);
            }
        }
        else
        {
            unsafe
            {
                var bufs = stackalloc DrawBufferMode[]
                {
                    DrawBufferMode.ColorAttachment0,
                    DrawBufferMode.ColorAttachment1,
                };
                gl.DrawBuffers(2, bufs);
            }
        }
    }

    private void DestroyGpuResources()
    {
        if (_fbo != 0)
        {
            gl.DeleteFramebuffer(_fbo);
            _fbo = 0;
        }

        if (_arrayTexture != 0)
        {
            gl.DeleteTexture(_arrayTexture);
            _arrayTexture = 0;
        }

        if (_occupancyTexture != 0)
        {
            gl.DeleteTexture(_occupancyTexture);
            _occupancyTexture = 0;
        }

        _width = 0;
        _height = 0;
        _slices = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyGpuResources();
    }
}
