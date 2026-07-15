using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Deterministic RGBA8 offscreen target for opt-in live GL pixel parity tests.
/// This class is never created by the production preview frame loop.
/// </summary>
internal sealed class GlPixelRenderHarness : IDisposable
{
    private readonly GL _gl;
    private readonly uint _framebuffer;
    private readonly uint _colorTexture;
    private readonly uint _depthRenderbuffer;
    private bool _disposed;

    public GlPixelRenderHarness(GL gl, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _gl = gl;
        Width = width;
        Height = height;

        _colorTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
        unsafe
        {
            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                (void*)0);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _depthRenderbuffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRenderbuffer);
        gl.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24,
            (uint)width,
            (uint)height);

        _framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _colorTexture,
            0);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer);
        gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
        gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            Dispose();
            throw new InvalidOperationException($"Pixel rendering harness framebuffer is incomplete: {status}.");
        }
    }

    public int Width { get; }

    public int Height { get; }

    public uint Framebuffer => _framebuffer;

    public GlPixelSnapshot Capture(
        string name,
        Action<GL> render,
        byte clearRed = 7,
        byte clearGreen = 11,
        byte clearBlue = 19,
        byte clearAlpha = 255)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(render);
        DrainErrors();

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
        _gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        ResetDeterministicState();
        _gl.ClearColor(clearRed / 255f, clearGreen / 255f, clearBlue / 255f, clearAlpha / 255f);
        _gl.ClearDepth(1.0);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        render(_gl);
        _gl.Finish();
        var renderError = _gl.GetError();
        if (renderError != GLEnum.NoError)
        {
            throw new InvalidOperationException($"Pixel rendering harness '{name}' produced GL error {renderError}.");
        }

        // A render callback may have used another target temporarily.
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
        _gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        var previousPackAlignment = _gl.GetInteger(GetPName.PackAlignment);
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        var bottomUp = new byte[checked(Width * Height * 4)];
        try
        {
            unsafe
            {
                fixed (byte* pixels = bottomUp)
                {
                    _gl.ReadPixels(
                        0,
                        0,
                        (uint)Width,
                        (uint)Height,
                        PixelFormat.Rgba,
                        PixelType.UnsignedByte,
                        pixels);
                }
            }

            var readError = _gl.GetError();
            if (readError != GLEnum.NoError)
            {
                throw new InvalidOperationException($"Pixel rendering harness '{name}' readback failed: {readError}.");
            }
        }
        finally
        {
            _gl.PixelStore(PixelStoreParameter.PackAlignment, previousPackAlignment);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        return GlPixelSnapshot.FromBottomUpRgba8(name, Width, Height, bottomUp);
    }

    private void ResetDeterministicState()
    {
        _gl.UseProgram(0);
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.StencilTest);
        _gl.Disable(EnableCap.Dither);
        _gl.Disable(EnableCap.Multisample);
        _gl.Disable(EnableCap.PolygonOffsetFill);
        _gl.ColorMask(true, true, true, true);
        _gl.DepthMask(true);
        _gl.DepthFunc(GLEnum.Lequal);
    }

    private void DrainErrors()
    {
        for (var i = 0; i < 16 && _gl.GetError() != GLEnum.NoError; i++)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_framebuffer != 0)
        {
            _gl.DeleteFramebuffer(_framebuffer);
        }

        if (_colorTexture != 0)
        {
            _gl.DeleteTexture(_colorTexture);
        }

        if (_depthRenderbuffer != 0)
        {
            _gl.DeleteRenderbuffer(_depthRenderbuffer);
        }
    }
}
