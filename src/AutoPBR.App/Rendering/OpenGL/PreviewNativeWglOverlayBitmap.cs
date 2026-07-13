namespace AutoPBR.App.Rendering.OpenGL;

internal sealed record PreviewNativeWglOverlayBitmap(
    int Width,
    int Height,
    byte[] BgraPremultiplied);
