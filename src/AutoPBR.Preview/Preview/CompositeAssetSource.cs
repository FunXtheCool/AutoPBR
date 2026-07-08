namespace AutoPBR.Preview;

internal sealed class CompositeAssetSource(params IAssetSource[] sources) : IAssetSource
{
    public bool Exists(string assetPath)
    {
        foreach (var s in sources)
        {
            if (s.Exists(assetPath))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryReadBytes(string assetPath, out byte[] bytes)
    {
        foreach (var s in sources)
        {
            if (s.TryReadBytes(assetPath, out bytes))
            {
                return true;
            }
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    public bool TryReadText(string assetPath, out string text)
    {
        foreach (var s in sources)
        {
            if (s.TryReadText(assetPath, out text))
            {
                return true;
            }
        }

        text = string.Empty;
        return false;
    }
}
