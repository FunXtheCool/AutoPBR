namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// ANGLE presentation D3D11 handles: native pointer for WGL_NV_DX_interop and MicroCom proxies for shared texture opens.
/// </summary>
internal readonly struct PreviewAngleD3D11Presentation
{
    public IntPtr NativeDevice { get; init; }

    public object? MicroComDevice { get; init; }

    public object? MicroComDevice1 { get; init; }

    public bool IsValid => NativeDevice != IntPtr.Zero && MicroComDevice is not null;
}
