using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlMeshBuffer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private int _indexCount;
    private DrawElementsType _indexElementType = DrawElementsType.UnsignedShort;
    private bool _disposed;

    public GlMeshBuffer(GL gl)
    {
        _gl = gl;
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();
    }

    public void Upload(float[] interleavedVertices, uint[] indices, int floatsPerVertex = 12)
    {
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData<float>(GLEnum.ArrayBuffer, interleavedVertices.AsSpan(), GLEnum.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        var maxIndex = 0u;
        foreach (var i in indices)
        {
            maxIndex = Math.Max(maxIndex, i);
        }

        if (maxIndex <= ushort.MaxValue)
        {
            var us = new ushort[indices.Length];
            for (var j = 0; j < indices.Length; j++)
            {
                us[j] = (ushort)indices[j];
            }

            _gl.BufferData<ushort>(GLEnum.ElementArrayBuffer, us.AsSpan(), GLEnum.StaticDraw);
            _indexElementType = DrawElementsType.UnsignedShort;
        }
        else
        {
            _gl.BufferData<uint>(GLEnum.ElementArrayBuffer, indices.AsSpan(), GLEnum.StaticDraw);
            _indexElementType = DrawElementsType.UnsignedInt;
        }

        var stride = (uint)(floatsPerVertex * sizeof(float));
        unsafe
        {
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
            if (floatsPerVertex >= 13)
            {
                _gl.EnableVertexAttribArray(4);
                _gl.VertexAttribIPointer(4, 1, VertexAttribIType.Int, stride, (void*)(12 * sizeof(float)));
            }
            else
            {
                _gl.DisableVertexAttribArray(4);
            }
        }

        _gl.BindVertexArray(0);
        _indexCount = indices.Length;
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        unsafe
        {
            _gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, _indexElementType, (void*)0);
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>Draw a subrange of the index buffer (indices are measured in elements, not bytes).</summary>
    public void DrawRange(int firstIndex, int indexCount)
    {
        if (indexCount <= 0)
        {
            return;
        }

        _gl.BindVertexArray(_vao);
        unsafe
        {
            var byteOffset = _indexElementType == DrawElementsType.UnsignedShort
                ? (void*)(firstIndex * sizeof(ushort))
                : (void*)(firstIndex * sizeof(uint));
            _gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount, _indexElementType, byteOffset);
        }

        _gl.BindVertexArray(0);
    }

    public int IndexCount => _indexCount;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
    }
}
