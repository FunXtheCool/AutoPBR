using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlImageLuminanceHistogram(GL gl) : IDisposable
{
    private const uint HistogramBinding = 0;
    private const uint SourceImageUnit = 0;
    private const uint ShaderStorageBarrierBit = 0x00002000;
    private const uint BufferUpdateBarrierBit = 0x00000200;
    private const int LocalSize = 8;

    private uint _buffer;
    private bool _disposed;

    public bool Dispatch(
        GlShaderProgram program,
        uint rgba8Texture,
        int width,
        int height,
        out GlLuminanceHistogramSnapshot snapshot,
        int sampleCapacity = GlLuminanceHistogramSnapshot.DefaultSampleCapacity)
    {
        snapshot = new GlLuminanceHistogramSnapshot(new uint[GlLuminanceHistogramSnapshot.BinCount], 0, 0);
        if (_disposed || !program.IsValid || rgba8Texture == 0 || width <= 0 || height <= 0 || sampleCapacity <= 0)
        {
            return false;
        }

        var stride = GlLuminanceHistogramSnapshot.ResolveSampleStride(width, height, sampleCapacity);
        var sampleWidth = (width + stride - 1) / stride;
        var sampleHeight = (height + stride - 1) / stride;
        Span<uint> zero = stackalloc uint[GlLuminanceHistogramSnapshot.DwordCount];
        zero.Clear();

        _buffer = _buffer == 0 ? gl.GenBuffer() : _buffer;
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _buffer);
        gl.BufferData<uint>(BufferTargetARB.ShaderStorageBuffer, zero, BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, HistogramBinding, _buffer);
        gl.BindImageTexture(SourceImageUnit, rgba8Texture, 0, false, 0, GLEnum.ReadOnly, GLEnum.Rgba8);

        program.Use();
        SetUniform2(program, "uSourceSize", width, height);
        SetUniform1(program, "uSampleStride", stride);
        SetUniform1(program, "uSampleCapacity", (uint)sampleCapacity);
        gl.DispatchCompute((uint)((sampleWidth + LocalSize - 1) / LocalSize),
            (uint)((sampleHeight + LocalSize - 1) / LocalSize), 1);
        gl.MemoryBarrier(ShaderStorageBarrierBit | BufferUpdateBarrierBit);

        var dwords = new uint[GlLuminanceHistogramSnapshot.DwordCount];
        gl.GetBufferSubData<uint>(BufferTargetARB.ShaderStorageBuffer, 0, dwords.AsSpan());
        gl.BindImageTexture(SourceImageUnit, 0, 0, false, 0, GLEnum.ReadOnly, GLEnum.Rgba8);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, HistogramBinding, 0);
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);

        snapshot = GlLuminanceHistogramSnapshot.FromDwords(dwords);
        return snapshot.IsConsistent && snapshot.OverflowCount == 0;
    }

    private void SetUniform1(GlShaderProgram program, string name, int value)
    {
        var location = program.GetUniformLocation(name);
        if (location >= 0)
        {
            gl.Uniform1(location, value);
        }
    }

    private void SetUniform1(GlShaderProgram program, string name, uint value)
    {
        var location = program.GetUniformLocation(name);
        if (location >= 0)
        {
            gl.Uniform1(location, value);
        }
    }

    private void SetUniform2(GlShaderProgram program, string name, int x, int y)
    {
        var location = program.GetUniformLocation(name);
        if (location >= 0)
        {
            gl.Uniform2(location, x, y);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_buffer != 0)
        {
            gl.DeleteBuffer(_buffer);
            _buffer = 0;
        }
    }
}
