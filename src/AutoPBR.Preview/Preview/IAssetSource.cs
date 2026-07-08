namespace AutoPBR.Preview;

internal interface IAssetSource
{
    bool Exists(string assetPath);
    bool TryReadBytes(string assetPath, out byte[] bytes);
    bool TryReadText(string assetPath, out string text);
}
