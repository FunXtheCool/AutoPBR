namespace AutoPBR.Tools.GeometryCompiler;

internal ref struct SpanReader
{
    private ReadOnlySpan<byte> _span;
    private int _pos;

    public SpanReader(ReadOnlySpan<byte> span)
    {
        _span = span;
        _pos = 0;
    }

    public int Position => _pos;

    public byte ReadU1()
    {
        if (_pos >= _span.Length)
        {
            throw new EndOfStreamException();
        }

        return _span[_pos++];
    }

    public ushort ReadU2()
    {
        if (_pos + 2 > _span.Length)
        {
            throw new EndOfStreamException();
        }

        var v = (ushort)((_span[_pos] << 8) | _span[_pos + 1]);
        _pos += 2;
        return v;
    }

    public uint ReadU4()
    {
        if (_pos + 4 > _span.Length)
        {
            throw new EndOfStreamException();
        }

        var v = (uint)((_span[_pos] << 24) | (_span[_pos + 1] << 16) | (_span[_pos + 2] << 8) | _span[_pos + 3]);
        _pos += 4;
        return v;
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        if (_pos + length > _span.Length)
        {
            throw new EndOfStreamException();
        }

        var slice = _span.Slice(_pos, length);
        _pos += length;
        return slice;
    }

    public void Skip(int bytes)
    {
        if (_pos + bytes > _span.Length)
        {
            throw new EndOfStreamException();
        }

        _pos += bytes;
    }
}
